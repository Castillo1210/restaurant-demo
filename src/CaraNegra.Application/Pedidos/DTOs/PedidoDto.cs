using CaraNegra.Application.Pagos.DTOs;
using CaraNegra.Domain.Enums;

namespace CaraNegra.Application.Pedidos.DTOs;

public class PedidoDto
{
    public int PedidoId { get; set; }
    public int MesaId { get; set; }
    public string MesaNumero { get; set; } = string.Empty;
    public int UsuarioId { get; set; }
    public string UsuarioNombre { get; set; } = string.Empty;
    public decimal SubTotal { get; set; }
    public decimal Total { get; set; }
    public EstadoPedido EstadoPedido { get; set; }
    public List<PedidoDetalleDto> Detalles { get; set; } = new();
    public List<PagoDto> Pagos { get; set; } = new();
    public DescuentoAplicadoDto? Descuento { get; set; }
    public DateTime CreadoEn { get; set; }
}

/// <summary>Descuento aplicado a un pedido (Fase 7). Null si el pedido no tiene ninguno.</summary>
public class DescuentoAplicadoDto
{
    public int DescuentoId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool EsPorcentaje { get; set; }
    public decimal Valor { get; set; }
    public decimal MontoDescuento { get; set; }
}

public class CreatePedidoDto
{
    public int MesaId { get; set; }
    public int UsuarioId { get; set; }
    public List<CreatePedidoDetalleDto> Detalles { get; set; } = new();
}

public class CreatePedidoDetalleDto
{
    public int ProductoId { get; set; }
    public int Cantidad { get; set; }
    public string? Notas { get; set; }
}

public class UpdatePedidoDto
{
    public int MesaId { get; set; }
    public int UsuarioId { get; set; }
}

public class UpdatePedidoEstadoDto
{
    public EstadoPedido EstadoPedido { get; set; }
}

/// <summary>Body para POST {id}/descuento (Fase 7): qué descuento del catálogo aplicar.</summary>
public class AplicarDescuentoDto
{
    public int DescuentoId { get; set; }
}

public class PedidoDetalleDto
{
    public int DetallePedidoId { get; set; }
    public int ProductoId { get; set; }
    public string ProductoNombre { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal Monto { get; set; }
    public string? Notas { get; set; }
    public EstadoDetallePedido EstadoDetallePedido { get; set; }
}