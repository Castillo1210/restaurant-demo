using CaraNegra.Domain.Common;

namespace CaraNegra.Domain.Entities;

/// <summary>
/// Arqueo de un medio de pago al cerrar la jornada: lo que el sistema esperaba frente a lo que
/// contó el cajero (arqueo ciego: el cajero escribe lo contado sin ver lo esperado).
/// </summary>
public class JornadaArqueo : BaseEntity
{
    public int JornadaArqueoId { get; set; }
    public int JornadaId { get; set; }

    public int? MetodoPagoId { get; set; }
    /// <summary>Nombre del medio de pago al momento del cierre.</summary>
    public string MetodoPago { get; set; } = string.Empty;
    public bool EsEfectivo { get; set; }

    /// <summary>Cobrado con este medio en la jornada.</summary>
    public decimal Ventas { get; set; }
    /// <summary>Lo que debería haber (en efectivo incluye fondo, ingresos y egresos).</summary>
    public decimal Esperado { get; set; }
    public decimal Contado { get; set; }
    /// <summary>Contado − esperado. Negativo = faltante, positivo = sobrante.</summary>
    public decimal Diferencia { get; set; }

    public Jornada Jornada { get; set; } = null!;
}
