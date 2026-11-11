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

    /// <summary>
    /// Versión de la foto del plato: 0 = sin foto; sube en 1 cada vez que se cambia. Va en la URL
    /// de la imagen (?v=N) para que los celulares la guarden en caché y la renueven al cambiarla.
    /// La foto en sí vive en la tabla ProductoImagenes (para no cargarla al listar la carta).
    /// </summary>
    public int ImagenVersion { get; set; }

    // Clave foranea
    public int CategoriaId { get; set; }

    // Propiedades de navegación
    public Categoria Categoria { get; set; } = null!;
    public ICollection<DetallePedido> DetallesPedidos { get; set; } = new List<DetallePedido>();
}