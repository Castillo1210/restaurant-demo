namespace CaraNegra.Application.Descuentos.DTOs;

public class DescuentoDto
{
    public int DescuentoId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool EsPorcentaje { get; set; }
    public decimal Valor { get; set; }
    public bool EstaActivo { get; set; }
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    public DateTime CreadoEn { get; set; }
}

public class CreateDescuentoDto
{
    public string Nombre { get; set; } = string.Empty;
    public bool EsPorcentaje { get; set; }
    public decimal Valor { get; set; }
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
}

public class UpdateDescuentoDto
{
    public string Nombre { get; set; } = string.Empty;
    public bool EsPorcentaje { get; set; }
    public decimal Valor { get; set; }
    public bool EstaActivo { get; set; }
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
}
