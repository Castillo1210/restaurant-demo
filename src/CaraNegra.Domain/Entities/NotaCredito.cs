using CaraNegra.Domain.Common;
using CaraNegra.Domain.Enums;

namespace CaraNegra.Domain.Entities;

/// <summary>
/// Nota de crédito electrónica sobre una boleta o factura ya aceptada. Tipos que usa el sistema
/// (catálogo 09 de SUNAT):
///   01 = Anulación de la operación: repite los mismos productos e importes del comprobante y lo
///        deja sin efecto (se usa cuando ya pasó el plazo de la comunicación de baja o del resumen).
///   03 = Corrección por error en la descripción: importes en cero, solo corrige textos.
/// </summary>
public class NotaCredito : BaseEntity
{
    public int NotaCreditoId { get; set; }

    /// <summary>Comprobante al que afecta.</summary>
    public int ComprobanteId { get; set; }

    public string Serie { get; set; } = string.Empty;   // FC01 / BC01
    public int Correlativo { get; set; }

    /// <summary>Código del tipo de nota: "01" anulación de la operación, "03" corrección de descripción.</summary>
    public string Codigo { get; set; } = "01";
    public string Motivo { get; set; } = string.Empty;

    /// <summary>Solo tipo 03: las descripciones corregidas, una por línea.</summary>
    public string? Correcciones { get; set; }

    public decimal OpGravada { get; set; }
    public decimal Igv { get; set; }
    public decimal Total { get; set; }

    public DateTime FechaEmision { get; set; } = DateTime.UtcNow;
    public EstadoComprobante Estado { get; set; } = EstadoComprobante.PorEnviar;
    public string Ambiente { get; set; } = "SIMULADO";
    public string HashResumen { get; set; } = string.Empty;
    public string? MensajeSunat { get; set; }
    public string? XmlUrl { get; set; }
    public string? CdrUrl { get; set; }
    public string? PdfUrl { get; set; }
    public int IntentosEnvio { get; set; }
    public DateTime? UltimoEnvioEn { get; set; }

    public int UsuarioId { get; set; }

    public Comprobante Comprobante { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
}
