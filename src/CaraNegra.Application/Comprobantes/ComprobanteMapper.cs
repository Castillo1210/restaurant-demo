using CaraNegra.Application.Common;
using CaraNegra.Application.Comprobantes.DTOs;
using CaraNegra.Domain.Entities;
using CaraNegra.Domain.Enums;

namespace CaraNegra.Application.Comprobantes;

/// <summary>
/// Convierte un Comprobante (con su Pedido, detalles, pagos y usuarios cargados) al DTO que
/// usa el frontend para dibujar la boleta/factura.
/// </summary>
public static class ComprobanteMapper
{
    public static string CodigoTipo(TipoComprobante tipo) => tipo == TipoComprobante.Factura ? "01" : "03";

    public static string CodigoDocumento(TipoDocumentoIdentidad tipo) => tipo switch
    {
        TipoDocumentoIdentidad.DNI => "1",
        TipoDocumentoIdentidad.CarnetExtranjeria => "4",
        TipoDocumentoIdentidad.RUC => "6",
        _ => "0"
    };

    public static string FormatearNumero(string serie, int correlativo) => $"{serie}-{correlativo:D8}";

    public static ComprobanteDto ToDto(Comprobante c, DatosEmpresa empresa)
    {
        var pedido = c.Pedido;

        var items = (pedido?.DetallesPedidos ?? new List<DetallePedido>())
            .Where(d => d.EstadoDetallePedido != EstadoDetallePedido.Cancelado)
            .GroupBy(d => new { d.ProductoId, d.Monto, Nombre = d.Producto?.Nombre ?? "Producto" })
            .Select(g => new ComprobanteItemDto
            {
                Descripcion = g.Key.Nombre,
                Cantidad = g.Sum(d => d.Cantidad),
                PrecioUnitario = g.Key.Monto,
                Importe = g.Sum(d => d.Cantidad * d.Monto)
            })
            .ToList();

        var pagos = (pedido?.Pagos ?? new List<Pago>())
            .Where(p => !p.EstaAnulado)
            .Select(p => new ComprobantePagoDto
            {
                MetodoPago = p.MetodoPago?.Nombre ?? string.Empty,
                Monto = p.Monto
            })
            .ToList();

        // Formato del QR según SUNAT:
        // RUC | TIPO | SERIE | NUMERO | IGV | TOTAL | FECHA | TIPO DOC ADQ | NRO DOC ADQ | HASH |
        var fechaLocal = PeruDateRangeHelper.ToHoraPeru(c.FechaEmision);
        var cadenaQr = string.Join("|",
            empresa.Ruc,
            CodigoTipo(c.TipoComprobante),
            c.Serie,
            c.Correlativo.ToString("D8"),
            c.Igv.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            c.Total.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            fechaLocal.ToString("yyyy-MM-dd"),
            CodigoDocumento(c.ClienteTipoDocumento),
            c.ClienteNumeroDocumento ?? "-",
            c.HashResumen) + "|";

        return new ComprobanteDto
        {
            ComprobanteId = c.ComprobanteId,
            PedidoId = c.PedidoId,
            MesaNumero = pedido?.Mesa?.NumeroMesa ?? string.Empty,
            TipoComprobante = c.TipoComprobante,
            CodigoTipo = CodigoTipo(c.TipoComprobante),
            Serie = c.Serie,
            Correlativo = c.Correlativo,
            Numero = FormatearNumero(c.Serie, c.Correlativo),
            ClienteTipoDocumento = c.ClienteTipoDocumento,
            ClienteNumeroDocumento = c.ClienteNumeroDocumento,
            ClienteNombre = c.ClienteNombre,
            ClienteDireccion = c.ClienteDireccion,
            OpGravada = c.OpGravada,
            Igv = c.Igv,
            IgvPorcentaje = empresa.IgvPorcentaje,
            Descuento = c.Descuento,
            Total = c.Total,
            FechaEmision = c.FechaEmision,
            HashResumen = c.HashResumen,
            CadenaQr = cadenaQr,
            Estado = c.Estado,
            EsSimulacion = c.EsSimulacion,
            AtendidoPor = pedido?.Usuario?.NombreCompleto ?? string.Empty,
            EmitidoPor = c.Usuario?.NombreCompleto ?? string.Empty,
            Emisor = new EmisorDto
            {
                Ruc = empresa.Ruc,
                RazonSocial = empresa.RazonSocial,
                NombreComercial = empresa.NombreComercial,
                Direccion = empresa.Direccion,
                Telefono = empresa.Telefono
            },
            Items = items,
            Pagos = pagos
        };
    }
}
