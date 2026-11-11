using CaraNegra.Application.Common;
using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Domain.Entities;
using CaraNegra.Domain.Enums;

namespace CaraNegra.Application.Comprobantes;

/// <summary>
/// Envío de un comprobante al proveedor de facturación electrónica (PSE) y registro de la
/// respuesta. Lo usan la emisión, el botón "Reintentar" y el proceso automático de pendientes.
///
/// Reglas para no duplicar ni perder comprobantes:
///   - Si un intento anterior quedó sin respuesta clara (sin internet, timeout), antes de reenviar
///     se CONSULTA el estado: puede que el proveedor sí lo haya recibido.
///   - Un "Pendiente" (SUNAT aún no responde) solo se consulta, nunca se reenvía.
///   - La venta nunca se bloquea: si no hay conexión el comprobante queda "PorEnviar" y se
///     reintenta solo.
/// Este método modifica el comprobante en memoria; quien lo llama hace SaveChanges.
/// </summary>
public static class ComprobanteEnvio
{
    private const int MaxMensaje = 500;
    private const int MaxUrl = 300;
    private const int MaxHash = 64;

    public static bool EsPendienteDeSunat(EstadoComprobante estado) =>
        estado is EstadoComprobante.PorEnviar or EstadoComprobante.Pendiente;

    /// <summary>Estados que se pueden reintentar a mano.</summary>
    public static bool SePuedeReintentar(EstadoComprobante estado) =>
        estado is EstadoComprobante.PorEnviar or EstadoComprobante.Pendiente or EstadoComprobante.ErrorEnvio;

    /// <summary>Estados que NO cuentan como comprobante válido del pedido (se puede emitir otro).</summary>
    public static bool NoEsValido(EstadoComprobante estado) =>
        estado is EstadoComprobante.Anulado or EstadoComprobante.Rechazado or EstadoComprobante.ErrorEnvio;

    public static SolicitudComprobante CrearSolicitud(Comprobante c) => new()
    {
        Tipo = c.TipoComprobante,
        Serie = c.Serie,
        Numero = c.Correlativo,
        FechaEmisionLocal = PeruDateRangeHelper.ToHoraPeru(c.FechaEmision),
        ClienteTipoDocumento = c.ClienteTipoDocumento,
        ClienteNumeroDocumento = c.ClienteNumeroDocumento,
        ClienteNombre = c.ClienteNombre,
        ClienteDireccion = c.ClienteDireccion,
        IgvPorcentaje = c.IgvPorcentaje,
        Total = c.Total,
        Items = ComprobanteMapper.ItemsDelPedido(c.Pedido)
            .Select(i => new ItemSolicitud
            {
                Descripcion = i.Descripcion,
                Cantidad = i.Cantidad,
                PrecioUnitario = i.PrecioUnitario
            })
            .ToList()
    };

    /// <summary>
    /// Envía el comprobante (o consulta su estado si ya se intentó) y guarda la respuesta en él.
    /// Requiere el comprobante con su Pedido → DetallesPedidos → Producto cargados.
    /// </summary>
    /// <param name="guardarAntesDeEnviar">
    /// Guarda en la base el intento (IntentosEnvio, UltimoEnvioEn) ANTES de hablar con el proveedor.
    /// Así, si la respuesta se pierde (corte, reinicio del servicio, petición cancelada), el siguiente
    /// intento sabe que este número ya pudo llegar y consulta su estado en vez de darlo por error.
    /// </param>
    public static async Task ProcesarAsync(Comprobante c, IFacturacionElectronicaService proveedor, CancellationToken ct,
        Func<CancellationToken, Task>? guardarAntesDeEnviar = null)
    {
        if (proveedor.EsSimulado) return;

        // Las líneas se arman con el pedido actual: si el pedido ya no es el que se facturó (otro
        // total, o dejó de estar cobrado), NO se envía, para no mandar a SUNAT algo distinto de lo
        // emitido. Con las reglas actuales no debería pasar (un pedido con comprobante válido ya no
        // se puede modificar), así que es solo una red de seguridad.
        if (c.Estado != EstadoComprobante.Pendiente && c.Pedido is { } pedido
            && (pedido.Total != c.Total || pedido.EstadoPedido != EstadoPedido.Entregado))
        {
            c.Estado = EstadoComprobante.ErrorEnvio;
            c.MensajeSunat = Recortar(
                $"No se envió: el pedido cambió después de emitir el comprobante (total del comprobante S/ {c.Total:0.00}, " +
                $"del pedido S/ {pedido.Total:0.00}). Revisa el pedido y emite uno nuevo si corresponde.", MaxMensaje);
            return;
        }

        var yaSeIntento = c.IntentosEnvio > 0;
        c.IntentosEnvio++;
        c.UltimoEnvioEn = DateTime.UtcNow;
        if (guardarAntesDeEnviar != null)
            await guardarAntesDeEnviar(ct);

        RespuestaFacturacion respuesta;

        if (c.Estado == EstadoComprobante.Pendiente || (c.Estado == EstadoComprobante.PorEnviar && yaSeIntento))
        {
            // Pudo haber llegado al proveedor: primero se consulta, y solo si no lo tiene se envía.
            respuesta = await proveedor.ConsultarEstadoAsync(c.TipoComprobante, c.Serie, c.Correlativo, ct);
            if (respuesta.Resultado == ResultadoFacturacion.NoEncontrado)
                respuesta = await proveedor.EnviarAsync(CrearSolicitud(c), ct);
        }
        else
        {
            respuesta = await proveedor.EnviarAsync(CrearSolicitud(c), ct);

            // "Ya fue emitido": si un intento anterior quedó sin respuesta, es este mismo
            // comprobante → se consulta su estado real. (Como el intento se guarda ANTES de enviar,
            // yaSeIntento es confiable aunque la respuesta se haya perdido.) En el PRIMER envío,
            // "ya existe" significa que el número lo tiene OTRA venta (base restaurada, serie
            // repetida): no se adopta el estado ajeno, queda como error con instrucciones.
            if (respuesta.Resultado == ResultadoFacturacion.Duplicado && yaSeIntento && c.Estado == EstadoComprobante.PorEnviar)
                respuesta = await proveedor.ConsultarEstadoAsync(c.TipoComprobante, c.Serie, c.Correlativo, ct);
        }

        Aplicar(c, respuesta);
    }

    private static void Aplicar(Comprobante c, RespuestaFacturacion r)
    {
        switch (r.Resultado)
        {
            case ResultadoFacturacion.Aceptado:
                c.Estado = EstadoComprobante.Aceptado;
                GuardarArchivos(c, r);
                c.MensajeSunat = Recortar(r.Mensaje, MaxMensaje);
                break;

            case ResultadoFacturacion.Pendiente:
                c.Estado = EstadoComprobante.Pendiente;
                GuardarArchivos(c, r);
                c.MensajeSunat = Recortar(r.Mensaje ?? "SUNAT aún no confirma el comprobante.", MaxMensaje);
                break;

            case ResultadoFacturacion.Rechazado:
                c.Estado = EstadoComprobante.Rechazado;
                GuardarArchivos(c, r);
                c.MensajeSunat = Recortar(r.Mensaje ?? "SUNAT rechazó el comprobante.", MaxMensaje);
                break;

            case ResultadoFacturacion.ErrorDatos:
                c.Estado = EstadoComprobante.ErrorEnvio;
                c.MensajeSunat = Recortar(r.Mensaje ?? "El proveedor no aceptó los datos del comprobante.", MaxMensaje);
                break;

            case ResultadoFacturacion.Duplicado:
                c.Estado = EstadoComprobante.ErrorEnvio;
                c.MensajeSunat = Recortar(
                    $"El número {ComprobanteMapper.FormatearNumero(c.Serie, c.Correlativo)} ya existe en el proveedor " +
                    $"({r.Mensaje}). Si reiniciaste la base de datos, usa otra serie en Datos de la empresa.", MaxMensaje);
                break;

            default: // SinConexion / NoEncontrado: se reintentará.
                if (c.Estado != EstadoComprobante.Pendiente)
                    c.Estado = EstadoComprobante.PorEnviar;
                c.MensajeSunat = Recortar(r.Mensaje ?? "No hubo respuesta del proveedor; se reintentará.", MaxMensaje);
                break;
        }
    }

    private static void GuardarArchivos(Comprobante c, RespuestaFacturacion r)
    {
        if (!string.IsNullOrWhiteSpace(r.Hash) && r.Hash.Length <= MaxHash) c.HashResumen = r.Hash;
        c.XmlUrl = Url(r.XmlUrl) ?? c.XmlUrl;
        c.CdrUrl = Url(r.CdrUrl) ?? c.CdrUrl;
        c.PdfUrl = Url(r.PdfUrl) ?? c.PdfUrl;
    }

    private static string? Url(string? url) =>
        string.IsNullOrWhiteSpace(url) || url.Length > MaxUrl ? null : url;

    private static string? Recortar(string? texto, int max) =>
        texto is null ? null : texto.Length <= max ? texto : texto[..max];
}
