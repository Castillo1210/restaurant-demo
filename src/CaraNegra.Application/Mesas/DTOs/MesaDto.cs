using CaraNegra.Domain.Enums;

namespace CaraNegra.Application.Mesas.DTOs;

public class MesaDto
{
    public int MesaId { get; set; }
    public string NumeroMesa { get; set; } = string.Empty;
    public EstadoMesa Estado { get; set; }
    public int? SalonId { get; set; }
    public string SalonNombre { get; set; } = string.Empty;
    public int SalonOrden { get; set; }
    public bool SalonActivo { get; set; } = true;

    // Pedido activo de la mesa (solo si está ocupada): quién la está atendiendo.
    public int? PedidoActivoId { get; set; }
    public int? AtendidoPorUsuarioId { get; set; }
    public string? AtendidoPor { get; set; }

    public DateTime CreadoEn { get; set; }
}

public class CreateMesaDto
{
    public string NumeroMesa { get; set; } = string.Empty;
    public int SalonId { get; set; }
}

public class UpdateMesaDto
{
    public string NumeroMesa { get; set; } = string.Empty;
    public EstadoMesa Estado { get; set; }
    public int SalonId { get; set; }
}
