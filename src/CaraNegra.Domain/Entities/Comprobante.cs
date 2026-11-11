using CaraNegra.Domain.Common;
using CaraNegra.Domain.Enums;

namespace CaraNegra.Domain.Entities;

/// <summary>
/// Comprobante de pago electrónico (boleta o factura) emitido para un pedido ya pagado.
///
/// Según el proveedor configurado (FacturacionElectronica:Proveedor):
///   - "Simulado": NO se envía a SUNAT (demo). Ambiente = SIMULADO.
///   - "ApiSunat" en sandbox: se envía al ambiente de PRUEBAS del PSE. Ambiente = SANDBOX.
///   - "ApiSunat" en producción: se envía a SUNAT de verdad. Ambiente = PRODUCCION.
/// EsSimulacion queda en true mientras el comprobante no tenga validez tributaria (todo lo que no
/// sea PRODUCCION), para que sea auditable a nivel de base de datos.
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

    // Datos del emisor AL MOMENTO de emitir (si luego cambian los datos de la empresa, los
    // comprobantes ya emitidos se siguen reimprimiendo tal como salieron).
    public string? EmisorRuc { get; set; }
    public string? EmisorRazonSocial { get; set; }
    public string? EmisorNombreComercial { get; set; }
    public string? EmisorDireccion { get; set; }
    public decimal IgvPorcentaje { get; set; }

    public DateTime FechaEmision { get; set; } = DateTime.UtcNow;
    public string HashResumen { get; set; } = string.Empty;
    public EstadoComprobante Estado { get; set; } = EstadoComprobante.AceptadoSimulado;
    public bool EsSimulacion { get; set; } = true;

    // ── Envío al proveedor de facturación electrónica (PSE) ──

    /// <summary>SIMULADO, SANDBOX o PRODUCCION (en qué ambiente se emitió).</summary>
    public string Ambiente { get; set; } = "SIMULADO";

    /// <summary>Último mensaje del proveedor / SUNAT (motivo de rechazo, error, etc.).</summary>
    public string? MensajeSunat { get; set; }

    /// <summary>Enlaces que devuelve el proveedor: XML firmado, CDR de SUNAT y PDF (ticket).</summary>
    public string? XmlUrl { get; set; }
    public string? CdrUrl { get; set; }
    public string? PdfUrl { get; set; }

    /// <summary>Veces que se intentó enviar/consultar (para reintentos automáticos).</summary>
    public int IntentosEnvio { get; set; }
    public DateTime? UltimoEnvioEn { get; set; }

    // Claves foráneas
    public int PedidoId { get; set; }
    public int UsuarioId { get; set; }

    // Navegación
    public Pedido Pedido { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
}
