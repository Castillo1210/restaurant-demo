using CaraNegra.Application.Pagos.DTOs;
using CaraNegra.Domain.Enums;

namespace CaraNegra.Application.Pedidos.DTOs;

public class PedidoDto
{
    public int PedidoId { get; set; }
    public int MesaId { get; set; }
    public string MesaNumero { get; set; } = string.Empty;
    public string SalonNombre { get; set; } = string.Empty;
    public int UsuarioId { get; set; }
    public string UsuarioNombre { get; set; } = string.Empty;
    public decimal SubTotal { get; set; }
    public decimal Total { get; set; }
    public EstadoPedido EstadoPedido { get; set; }
    public List<PedidoDetalleDto> Detalles { get; set; } = new();
    public List<PagoDto> Pagos { get; set; } = new();
    public DescuentoAplicadoDto? Descuento { get; set; }
    public string? Comentario { get; set; }
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

    /// <summary>Comentario general del pedido (opcional, hasta 300 caracteres).</summary>
    public string? Comentario { get; set; }

    /// <summary>Código único del envío (lo genera la tablet). Un reintento con el mismo código no duplica.</summary>
    public string? OperacionId { get; set; }
}

/// <summary>Body para PUT {id}/comentario. Vacío o null borra el comentario.</summary>
public class ActualizarComentarioPedidoDto
{
    public string? Comentario { get; set; }
}

public class CreatePedidoDetalleDto
{
    public int ProductoId { get; set; }
    public int Cantidad { get; set; }
    public string? Notas { get; set; }

    /// <summary>
    /// Precio distinto al de la carta (opcional). Solo lo aceptan el CAJERO y el ADMIN; queda
    /// registrado en CambiosPrecioPedido.
    /// </summary>
    public decimal? PrecioUnitario { get; set; }
}

/// <summary>Body para PUT {id}/detalles/{detalleId}/precio (solo cajero / admin).</summary>
public class CambiarPrecioDetalleDto
{
    public decimal Precio { get; set; }
    public string? Motivo { get; set; }
}

/// <summary>Body para POST {id}/detalles/lote: todo el carrito del mozo en una sola petición.</summary>
public class AgregarDetallesDto
{
    public List<CreatePedidoDetalleDto> Detalles { get; set; } = new();

    /// <summary>Comentario general del pedido: null = sin cambios; vacío lo borra.</summary>
    public string? Comentario { get; set; }

    /// <summary>Código único del envío (lo genera la tablet). Un reintento con el mismo código no duplica.</summary>
    public string? OperacionId { get; set; }
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

/// <summary>Cuerpo de POST pedidos/{id}/anular.</summary>
public class AnularPedidoDto
{
    public string? Motivo { get; set; }

    /// <summary>Imprimir "PEDIDO ANULADO" en cocina. Si no se envía, true.</summary>
    public bool AvisarCocina { get; set; } = true;
}
