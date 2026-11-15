namespace CaraNegra.Application.Categorias.DTOs;

public class CategoriaDto
{
    public int CategoriaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public bool EstaActivo { get; set; }
    /// <summary>Venta de mostrador: sus productos no van a cocina y salen en "Venta rápida".</summary>
    public bool EsMostrador { get; set; }
    public DateTime CreadoEn { get; set; }
}

public class CreateCategoriaDto
{
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public bool EsMostrador { get; set; }
}

public class UpdateCategoriaDto
{
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public bool EstaActivo { get; set; }
    public bool EsMostrador { get; set; }
}