using CaraNegra.Domain.Common;

namespace CaraNegra.Domain.Entities;

public class Descuento : BaseEntity, ISoftDeletable
{
    public int Descuentoid { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool EsPorcentaje { get; set; } // true = %
    public decimal Valor { get; set; }
    public bool EstaActivo { get; set; } = true;
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }

    // Propiedades de navegación
    public ICollection<DetalleDescuento> DetallesDescuentos { get; set; } = new List<DetalleDescuento>();
}