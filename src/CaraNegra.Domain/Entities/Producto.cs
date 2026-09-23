using CaraNegra.Domain.Common;

namespace CaraNegra.Domain.Entities;

public class Producto : BaseEntity
{
    public int ProductoId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }
    public bool EstaDisponible { get; set; } = true;
    public string Tipo { get; set; } = string.Empty;

    // Clave foranea
    public int CategoriaId { get; set; }

    // Propiedades de navegación
    public Categoria Categoria { get; set; } = null!;
    public ICollection<DetallePedido> DetallesPedidos { get; set; } = new List<DetallePedido>();
}