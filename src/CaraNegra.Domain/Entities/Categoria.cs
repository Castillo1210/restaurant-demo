using CaraNegra.Domain.Common;

namespace CaraNegra.Domain.Entities;

public class Categoria : BaseEntity, ISoftDeletable
{
    public int CategoriaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public bool EstaActivo { get; set; } = true;

    /// <summary>
    /// Venta de mostrador (entradas de la piscina, ropa de baño, flotadores…): sus productos no van
    /// a cocina (sin comanda ni pantalla de cocina) y se venden en la pantalla "Venta rápida".
    /// </summary>
    public bool EsMostrador { get; set; }

    // Propiedades de navegacion
    public ICollection<Producto> Productos { get; set; } = new List<Producto>();
    public ICollection<Articulo> Articulos { get; set; } = new List<Articulo>();
}