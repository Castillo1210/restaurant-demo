namespace CaraNegra.Application.Reportes.DTOs;

public class ProductoMasVendidoDto
{
    public int ProductoId { get; set; }
    public string ProductoNombre { get; set; } = string.Empty;
    public string CategoriaNombre { get; set; } = string.Empty;
    public int CantidadVendida { get; set; }
    public decimal TotalVendido { get; set; }
}
