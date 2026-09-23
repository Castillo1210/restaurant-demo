using CaraNegra.Domain.Enums;

namespace CaraNegra.Application.Cocina.DTOs;

/// <summary>Pedido tal como lo ve la pantalla de cocina (KDS): sin precios, con tiempos.</summary>
public class PedidoCocinaDto
{
    public int PedidoId { get; set; }
    public string MesaNumero { get; set; } = string.Empty;
    public string MozoNombre { get; set; } = string.Empty;
    public EstadoPedido EstadoPedido { get; set; }
    public DateTime CreadoEn { get; set; }
    public DateTime? ActualizadoEn { get; set; }
    public List<ItemCocinaDto> Items { get; set; } = new();
}

public class ItemCocinaDto
{
    public int DetallePedidoId { get; set; }
    public int ProductoId { get; set; }
    public string ProductoNombre { get; set; } = string.Empty;
    public string? CategoriaNombre { get; set; }
    public int Cantidad { get; set; }
    public string? Notas { get; set; }
    public EstadoDetallePedido Estado { get; set; }
    public DateTime CreadoEn { get; set; }
}

public class CambiarEstadoItemCocinaDto
{
    public EstadoDetallePedido Estado { get; set; }
}

public class CambiarEstadoPedidoCocinaDto
{
    public EstadoPedido Estado { get; set; }
}
