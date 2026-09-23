using CaraNegra.Domain.Common;
using CaraNegra.Domain.Enums;

namespace CaraNegra.Domain.Entities;

/// <summary>
/// Comprobante de pago electrónico (boleta o factura) emitido para un pedido ya pagado.
///
/// IMPORTANTE (DEMO): este comprobante es una SIMULACIÓN. Tiene el formato y los datos de un
/// comprobante electrónico peruano (serie-correlativo, IGV, QR), pero NO se firma con un
/// certificado digital ni se envía a SUNAT/OSE, por lo que no tiene validez tributaria.
/// El campo EsSimulacion queda en true para que esto sea auditable a nivel de base de datos.
/// </summary>
public class Comprobante : BaseEntity
{
    public int ComprobanteId { get; set; }

    public TipoComprobante TipoComprobante { get; set; }
    public string Serie { get; set; } = string.Empty;   // B001 / F001
    public int Correlativo { get; set; }                // 1, 2, 3... (se muestra con 8 dígitos)

    // Datos del cliente (adquiriente)
    public TipoDocumentoIdentidad ClienteTipoDocumento { get; set; } = TipoDocumentoIdentidad.SinDocumento;
    public string? ClienteNumeroDocumento { get; set; }
    public string ClienteNombre { get; set; } = string.Empty;  // Nombre o razón social
    public string? ClienteDireccion { get; set; }

    // Importes (los precios de la carta incluyen IGV)
    public decimal OpGravada { get; set; }
    public decimal Igv { get; set; }
    public decimal Descuento { get; set; }
    public decimal Total { get; set; }

    public DateTime FechaEmision { get; set; } = DateTime.UtcNow;
    public string HashResumen { get; set; } = string.Empty;
    public EstadoComprobante Estado { get; set; } = EstadoComprobante.AceptadoSimulado;
    public bool EsSimulacion { get; set; } = true;

    // Claves foráneas
    public int PedidoId { get; set; }
    public int UsuarioId { get; set; }

    // Navegación
    public Pedido Pedido { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
}
