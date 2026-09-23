using CaraNegra.Domain.Enums;

namespace CaraNegra.Application.Comprobantes.DTOs;

public class ComprobanteDto
{
    public int ComprobanteId { get; set; }
    public int PedidoId { get; set; }
    public string MesaNumero { get; set; } = string.Empty;

    public TipoComprobante TipoComprobante { get; set; }
    /// <summary>Código SUNAT del tipo: "03" boleta, "01" factura.</summary>
    public string CodigoTipo { get; set; } = string.Empty;
    public string Serie { get; set; } = string.Empty;
    public int Correlativo { get; set; }
    /// <summary>Serie-correlativo formateado, p.ej. "B001-00000123".</summary>
    public string Numero { get; set; } = string.Empty;

    public TipoDocumentoIdentidad ClienteTipoDocumento { get; set; }
    public string? ClienteNumeroDocumento { get; set; }
    public string ClienteNombre { get; set; } = string.Empty;
    public string? ClienteDireccion { get; set; }

    public decimal OpGravada { get; set; }
    public decimal Igv { get; set; }
    public decimal IgvPorcentaje { get; set; }
    public decimal Descuento { get; set; }
    public decimal Total { get; set; }

    public DateTime FechaEmision { get; set; }
    public string HashResumen { get; set; } = string.Empty;
    /// <summary>Contenido del QR en el formato definido por SUNAT (campos separados por "|").</summary>
    public string CadenaQr { get; set; } = string.Empty;
    public EstadoComprobante Estado { get; set; }
    public bool EsSimulacion { get; set; }

    public string AtendidoPor { get; set; } = string.Empty;
    public string EmitidoPor { get; set; } = string.Empty;

    public EmisorDto Emisor { get; set; } = new();
    public List<ComprobanteItemDto> Items { get; set; } = new();
    public List<ComprobantePagoDto> Pagos { get; set; } = new();
}

public class EmisorDto
{
    public string Ruc { get; set; } = string.Empty;
    public string RazonSocial { get; set; } = string.Empty;
    public string NombreComercial { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;
    public string? Telefono { get; set; }
}

public class ComprobanteItemDto
{
    public string Descripcion { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Importe { get; set; }
}

public class ComprobantePagoDto
{
    public string MetodoPago { get; set; } = string.Empty;
    public decimal Monto { get; set; }
}

/// <summary>Body de POST /comprobantes.</summary>
public class EmitirComprobanteDto
{
    public int PedidoId { get; set; }
    public TipoComprobante TipoComprobante { get; set; }
    public TipoDocumentoIdentidad ClienteTipoDocumento { get; set; } = TipoDocumentoIdentidad.SinDocumento;
    public string? ClienteNumeroDocumento { get; set; }
    public string? ClienteNombre { get; set; }
    public string? ClienteDireccion { get; set; }
}
