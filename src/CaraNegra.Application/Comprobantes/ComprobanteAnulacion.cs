using System.Security.Cryptography;
using System.Text;
using CaraNegra.Application.Common;
using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Domain.Entities;
using CaraNegra.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Comprobantes;

/// <summary>
/// Reglas para anular un comprobante ante SUNAT (con los plazos de APISUNAT):
///   - Factura, hasta 3 días desde su emisión → comunicación de baja.
///   - Boleta, hasta 5 días desde su emisión  → resumen diario con acción "anular".
///   - Fuera de esos plazos (o si la baja fue rechazada) → nota de crédito 01 "Anulación de la operación".
/// Solo se anula un comprobante ACEPTADO. Mientras la anulación no esté aceptada, el comprobante
/// sigue vigente (cuenta en los totales y su pedido no se puede tocar).
/// </summary>
public static class ComprobanteAnulacion
{
    public const string Baja = "BAJA";
    public const string Resumen = "RESUMEN";
    public const string ConNotaCredito = "NOTA_CREDITO";

    public const int DiasBajaFactura = 3;
    public const int DiasResumenBoleta = 5;

    public const string CodigoAnulacionOperacion = "01";
    public const string CodigoCorreccionDescripcion = "03";

    public const int MotivoMinimo = 3;
    public const int MotivoMaximo = 100;

    /// <summary>Días calendario (hora de Lima) desde la emisión: 0 = emitido hoy.</summary>
    public static int DiasDesdeEmision(DateTime fechaEmisionUtc) =>
        (PeruDateRangeHelper.ToHoraPeru(DateTime.UtcNow).Date - PeruDateRangeHelper.ToHoraPeru(fechaEmisionUtc).Date).Days;

    /// <summary>Cómo se anularía hoy (sin contar si ya hay una anulación en curso).</summary>
    public static string MetodoPara(Comprobante c)
    {
        var dias = DiasDesdeEmision(c.FechaEmision);
        return c.TipoComprobante == TipoComprobante.Factura
            ? (dias <= DiasBajaFactura ? Baja : ConNotaCredito)
            : (dias <= DiasResumenBoleta ? Resumen : ConNotaCredito);
    }

    public static bool EstaVigente(EstadoComprobante estado) =>
        estado is EstadoComprobante.Aceptado or EstadoComprobante.AceptadoSimulado;

    public static bool EnCurso(Comprobante c) =>
        c.AnulacionEstado is EstadoAnulacion.PorEnviar or EstadoAnulacion.Pendiente;

    /// <summary>Por qué no se puede anular (null si se puede).</summary>
    public static string? MotivoNoSePuede(Comprobante c)
    {
        if (c.Estado == EstadoComprobante.Anulado) return "El comprobante ya está anulado.";
        if (EnCurso(c)) return "La anulación de este comprobante ya está en proceso.";
        if (c.Estado is EstadoComprobante.PorEnviar or EstadoComprobante.Pendiente)
            return "SUNAT todavía no confirma este comprobante: espera a que quede aceptado para anularlo.";
        if (!EstaVigente(c.Estado))
            return "Este comprobante no es válido (rechazado o con error de envío): no hace falta anularlo.";
        return null;
    }

    public static string DescripcionMetodo(string? metodo) => metodo switch
    {
        Baja => "comunicación de baja",
        Resumen => "resumen diario",
        ConNotaCredito => "nota de crédito",
        _ => "anulación"
    };

    public static string DescripcionCodigo(string codigo) => codigo switch
    {
        CodigoAnulacionOperacion => "Anulación de la operación",
        CodigoCorreccionDescripcion => "Corrección por error en la descripción",
        _ => $"Tipo {codigo}"
    };

    /// <summary>Último número de una serie de notas de crédito (esta base o el sistema anterior).</summary>
    public static async Task<int> UltimoNumeroNotaAsync(IApplicationDbContext context, IFacturacionElectronicaService facturacion,
        string serie, CancellationToken ct)
    {
        var enEsteSistema = await context.NotasCredito
            .Where(n => n.Serie == serie)
            .MaxAsync(n => (int?)n.Correlativo, ct) ?? 0;
        return Math.Max(enEsteSistema, facturacion.UltimoNumeroAnterior(serie));
    }

    /// <summary>Hash de ejemplo para el modo Simulado (en real lo devuelve el proveedor).</summary>
    public static string HashSimulado(string datos) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(datos)))[..28];

    /// <summary>
    /// Refleja en el comprobante el estado de la nota de crédito 01 con la que se está anulando.
    /// </summary>
    public static void SincronizarConNota(Comprobante c, NotaCredito nota)
    {
        if (c.AnulacionMetodo != ConNotaCredito || nota.Codigo != CodigoAnulacionOperacion) return;

        var numero = ComprobanteMapper.FormatearNumero(nota.Serie, nota.Correlativo);
        switch (nota.Estado)
        {
            case EstadoComprobante.Aceptado:
            case EstadoComprobante.AceptadoSimulado:
                c.Estado = EstadoComprobante.Anulado;
                c.AnulacionEstado = EstadoAnulacion.Aceptada;
                c.AnulacionMensaje = $"Anulado con la nota de crédito {numero}.";
                break;
            case EstadoComprobante.Pendiente:
                c.AnulacionEstado = EstadoAnulacion.Pendiente;
                c.AnulacionMensaje = $"Nota de crédito {numero} enviada; falta que SUNAT la confirme.";
                break;
            case EstadoComprobante.PorEnviar:
                c.AnulacionEstado = EstadoAnulacion.PorEnviar;
                c.AnulacionMensaje = $"La nota de crédito {numero} aún no llega al proveedor; se reintenta sola.";
                break;
            default:
                c.AnulacionEstado = EstadoAnulacion.Rechazada;
                c.AnulacionMensaje = ComprobanteEnvio.Recortar(
                    $"Nota de crédito {numero} no aceptada: {nota.MensajeSunat}", 500);
                break;
        }
    }
}

/// <summary>
/// Envío de la comunicación de baja / resumen diario y lectura del resultado. Modifica el
/// comprobante en memoria; quien lo llama hace SaveChanges.
/// </summary>
public static class AnulacionEnvio
{
    public static async Task ProcesarAsync(Comprobante c, IFacturacionElectronicaService proveedor, CancellationToken ct,
        Func<CancellationToken, Task>? guardarAntesDeEnviar = null)
    {
        if (proveedor.EsSimulado) return;
        if (c.AnulacionMetodo is not (ComprobanteAnulacion.Baja or ComprobanteAnulacion.Resumen)) return;
        if (!ComprobanteAnulacion.EnCurso(c)) return;

        var yaSeIntento = c.AnulacionIntentos > 0;
        c.AnulacionIntentos++;
        c.AnulacionUltimoEnvioEn = DateTime.UtcNow;
        if (guardarAntesDeEnviar != null)
            await guardarAntesDeEnviar(ct);

        var solicitud = new SolicitudAnulacion
        {
            Tipo = c.TipoComprobante,
            Serie = c.Serie,
            Numero = c.Correlativo,
            Motivo = c.AnulacionMotivo ?? "ANULACIÓN DE LA OPERACIÓN"
        };

        if (c.AnulacionEstado == EstadoAnulacion.Pendiente || yaSeIntento)
        {
            // Ya se envió antes: se consulta el comprobante. Si figura anulado, la baja se aceptó.
            var consulta = await proveedor.ConsultarEstadoAsync(c.TipoComprobante, c.Serie, c.Correlativo, ct);
            if (consulta.Resultado == ResultadoFacturacion.Anulado)
            {
                Aceptar(c, consulta.Mensaje);
                return;
            }

            if (c.AnulacionEstado == EstadoAnulacion.Pendiente)
            {
                c.AnulacionMensaje = "SUNAT todavía no confirma la anulación; se vuelve a consultar sola.";
                return;
            }
            // PorEnviar con intentos anteriores sin respuesta: se vuelve a enviar.
        }

        Aplicar(c, await proveedor.AnularAsync(solicitud, ct));
    }

    private static void Aceptar(Comprobante c, string? mensaje)
    {
        c.Estado = EstadoComprobante.Anulado;
        c.AnulacionEstado = EstadoAnulacion.Aceptada;
        c.AnulacionMensaje = ComprobanteEnvio.Recortar(
            mensaje ?? $"SUNAT aceptó la {ComprobanteAnulacion.DescripcionMetodo(c.AnulacionMetodo)}.", 500);
    }

    private static void Aplicar(Comprobante c, RespuestaFacturacion r)
    {
        switch (r.Resultado)
        {
            case ResultadoFacturacion.Aceptado:
            case ResultadoFacturacion.Anulado:
                Aceptar(c, r.Mensaje);
                break;

            case ResultadoFacturacion.Pendiente:
            case ResultadoFacturacion.Duplicado: // "ya fue enviado": se queda esperando la confirmación
                c.AnulacionEstado = EstadoAnulacion.Pendiente;
                c.AnulacionMensaje = ComprobanteEnvio.Recortar(
                    r.Mensaje ?? "Anulación enviada; falta que SUNAT la confirme.", 500);
                break;

            case ResultadoFacturacion.Rechazado:
            case ResultadoFacturacion.ErrorDatos:
                c.AnulacionEstado = EstadoAnulacion.Rechazada;
                c.AnulacionMensaje = ComprobanteEnvio.Recortar(
                    r.Mensaje ?? "El proveedor no aceptó la anulación.", 500);
                break;

            default: // SinConexion / NoEncontrado
                if (c.AnulacionEstado != EstadoAnulacion.Pendiente)
                    c.AnulacionEstado = EstadoAnulacion.PorEnviar;
                c.AnulacionMensaje = ComprobanteEnvio.Recortar(
                    r.Mensaje ?? "No hubo respuesta del proveedor; se reintentará.", 500);
                break;
        }
    }
}

/// <summary>Envío de una nota de crédito y registro de la respuesta (como ComprobanteEnvio).</summary>
public static class NotaCreditoEnvio
{
    /// <summary>
    /// Requiere el comprobante afectado con su Pedido → DetallesPedidos → Producto cargados
    /// (la nota 01 repite sus productos).
    /// </summary>
    public static SolicitudComprobante CrearSolicitud(NotaCredito n, Comprobante c)
    {
        var esCorreccion = n.Codigo == ComprobanteAnulacion.CodigoCorreccionDescripcion;

        var items = esCorreccion
            ? LineasCorreccion(n.Correcciones)
                .Select(l => new ItemSolicitud { Descripcion = l, Cantidad = 1, PrecioUnitario = 0 })
                .ToList()
            : ComprobanteMapper.ItemsDelPedido(c.Pedido)
                .Select(i => new ItemSolicitud { Descripcion = i.Descripcion, Cantidad = i.Cantidad, PrecioUnitario = i.PrecioUnitario })
                .ToList();

        return new SolicitudComprobante
        {
            Tipo = c.TipoComprobante,
            Serie = n.Serie,
            Numero = n.Correlativo,
            FechaEmisionLocal = PeruDateRangeHelper.ToHoraPeru(n.FechaEmision),
            ClienteTipoDocumento = c.ClienteTipoDocumento,
            ClienteNumeroDocumento = c.ClienteNumeroDocumento,
            ClienteNombre = c.ClienteNombre,
            ClienteDireccion = c.ClienteDireccion,
            IgvPorcentaje = c.IgvPorcentaje,
            Total = n.Total,
            Items = items,
            NotaCredito = new DatosNotaCredito
            {
                Codigo = n.Codigo,
                Motivo = n.Motivo,
                AfectadoTipo = c.TipoComprobante,
                AfectadoSerie = c.Serie,
                AfectadoNumero = c.Correlativo
            }
        };
    }

    public static List<string> LineasCorreccion(string? correcciones) =>
        (correcciones ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

    public static async Task ProcesarAsync(NotaCredito n, Comprobante c, IFacturacionElectronicaService proveedor,
        CancellationToken ct, Func<CancellationToken, Task>? guardarAntesDeEnviar = null)
    {
        if (proveedor.EsSimulado) return;

        // La nota 01 repite los productos del pedido: si el pedido cambió, no se envía algo distinto.
        if (n.Codigo == ComprobanteAnulacion.CodigoAnulacionOperacion && n.Estado != EstadoComprobante.Pendiente
            && c.Pedido is { } pedido && pedido.Total != n.Total)
        {
            n.Estado = EstadoComprobante.ErrorEnvio;
            n.MensajeSunat = $"No se envió: el pedido cambió después de emitir el comprobante (nota S/ {n.Total:0.00}, pedido S/ {pedido.Total:0.00}).";
            return;
        }

        var yaSeIntento = n.IntentosEnvio > 0;
        n.IntentosEnvio++;
        n.UltimoEnvioEn = DateTime.UtcNow;
        if (guardarAntesDeEnviar != null)
            await guardarAntesDeEnviar(ct);

        RespuestaFacturacion r;
        if (n.Estado == EstadoComprobante.Pendiente || (n.Estado == EstadoComprobante.PorEnviar && yaSeIntento))
        {
            r = await proveedor.ConsultarNotaCreditoAsync(n.Serie, n.Correlativo, ct);
            if (r.Resultado == ResultadoFacturacion.NoEncontrado)
                r = await proveedor.EmitirNotaCreditoAsync(CrearSolicitud(n, c), ct);
        }
        else
        {
            r = await proveedor.EmitirNotaCreditoAsync(CrearSolicitud(n, c), ct);
            if (r.Resultado == ResultadoFacturacion.Duplicado && yaSeIntento)
                r = await proveedor.ConsultarNotaCreditoAsync(n.Serie, n.Correlativo, ct);
        }

        Aplicar(n, r);
    }

    private static void Aplicar(NotaCredito n, RespuestaFacturacion r)
    {
        switch (r.Resultado)
        {
            case ResultadoFacturacion.Aceptado:
                n.Estado = EstadoComprobante.Aceptado;
                Archivos(n, r);
                n.MensajeSunat = ComprobanteEnvio.Recortar(r.Mensaje, 500);
                break;
            case ResultadoFacturacion.Pendiente:
                n.Estado = EstadoComprobante.Pendiente;
                Archivos(n, r);
                n.MensajeSunat = ComprobanteEnvio.Recortar(r.Mensaje ?? "SUNAT aún no confirma la nota de crédito.", 500);
                break;
            case ResultadoFacturacion.Rechazado:
                n.Estado = EstadoComprobante.Rechazado;
                Archivos(n, r);
                n.MensajeSunat = ComprobanteEnvio.Recortar(r.Mensaje ?? "SUNAT rechazó la nota de crédito.", 500);
                break;
            case ResultadoFacturacion.ErrorDatos:
            case ResultadoFacturacion.Duplicado:
                n.Estado = EstadoComprobante.ErrorEnvio;
                n.MensajeSunat = ComprobanteEnvio.Recortar(r.Mensaje ?? "El proveedor no aceptó la nota de crédito.", 500);
                break;
            default:
                if (n.Estado != EstadoComprobante.Pendiente)
                    n.Estado = EstadoComprobante.PorEnviar;
                n.MensajeSunat = ComprobanteEnvio.Recortar(r.Mensaje ?? "No hubo respuesta del proveedor; se reintentará.", 500);
                break;
        }
    }

    private static void Archivos(NotaCredito n, RespuestaFacturacion r)
    {
        if (!string.IsNullOrWhiteSpace(r.Hash) && r.Hash.Length <= 64) n.HashResumen = r.Hash;
        n.XmlUrl = ComprobanteEnvio.Url(r.XmlUrl) ?? n.XmlUrl;
        n.CdrUrl = ComprobanteEnvio.Url(r.CdrUrl) ?? n.CdrUrl;
        n.PdfUrl = ComprobanteEnvio.Url(r.PdfUrl) ?? n.PdfUrl;
    }
}
