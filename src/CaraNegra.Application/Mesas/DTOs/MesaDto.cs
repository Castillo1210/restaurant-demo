using CaraNegra.Domain.Enums;

namespace CaraNegra.Application.Mesas.DTOs;

public class MesaDto
{
    public int MesaId { get; set; }
    public string NumeroMesa { get; set; } = string.Empty;
    public EstadoMesa Estado { get; set; }
    public DateTime CreadoEn { get; set; }
}

public class CreateMesaDto
{
    public string NumeroMesa { get; set; } = string.Empty;
}

public class UpdateMesaDto
{
    public string NumeroMesa { get; set; } = string.Empty;
    public EstadoMesa Estado { get; set; }
}