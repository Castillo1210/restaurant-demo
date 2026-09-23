namespace CaraNegra.Application.Cremas.DTOs;

public class CremaDto
{
    public int CremaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int Orden { get; set; }
    public bool EstaActivo { get; set; }
    public DateTime CreadoEn { get; set; }
}

public class CreateCremaDto
{
    public string Nombre { get; set; } = string.Empty;
}

public class UpdateCremaDto
{
    public string Nombre { get; set; } = string.Empty;
    public int Orden { get; set; }
    public bool EstaActivo { get; set; }
}
