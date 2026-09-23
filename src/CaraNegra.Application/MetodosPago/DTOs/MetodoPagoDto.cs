namespace CaraNegra.Application.MetodosPago.DTOs;

public class MetodoPagoDto
{
    public int MetodoPagoId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool EstaActivo { get; set; }
    public DateTime CreadoEn { get; set; }
}

public class CreateMetodoPagoDto
{
    public string Nombre { get; set; } = string.Empty;
}

public class UpdateMetodoPagoDto
{
    public string Nombre { get; set; } = string.Empty;
    public bool EstaActivo { get; set; }
}
