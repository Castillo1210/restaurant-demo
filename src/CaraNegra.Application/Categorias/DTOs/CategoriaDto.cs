namespace CaraNegra.Application.Categorias.DTOs;

public class CategoriaDto
{
    public int CategoriaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public bool EstaActivo { get; set; }
    public DateTime CreadoEn { get; set; }
}

public class CreateCategoriaDto
{
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
}

public class UpdateCategoriaDto
{
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public bool EstaActivo { get; set; }
}