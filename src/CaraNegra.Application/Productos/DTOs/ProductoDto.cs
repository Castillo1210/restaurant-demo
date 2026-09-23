namespace CaraNegra.Application.Productos.DTOs;

public class ProductoDto
{
    public int ProductoId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }
    public bool EstaDisponible { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public int CategoriaId { get; set; }
    public string CategoriaNombre { get; set; } = string.Empty;
    public DateTime CreadoEn { get; set; }
}

public class CreateProductoDto
{
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public int CategoriaId { get; set; }
}

public class UpdateProductoDto
{
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }
    public bool EstaDisponible { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public int CategoriaId { get; set; }
}