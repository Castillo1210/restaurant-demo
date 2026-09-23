using CaraNegra.Domain.Common;

namespace CaraNegra.Domain.Entities;

public class Articulo : BaseEntity
{
    public int ArticuloId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }
    public int Stock { get; set; }
    public bool Activo { get; set; } = true;
    public string Tipo { get; set; } = string.Empty;

    // Clave foranea
    public int CategoriaId { get; set; }

    // Propiedades de navegacion
    public Categoria Categoria { get; set; } = null!;
    public ICollection<MovimientoArticulo> MovimientosArticulos { get; set; } = new List<MovimientoArticulo>();
}