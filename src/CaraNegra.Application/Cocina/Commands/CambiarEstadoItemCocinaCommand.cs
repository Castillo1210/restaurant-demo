using CaraNegra.Application.Cocina.DTOs;
using CaraNegra.Application.Cocina.Queries;
using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Cocina.Commands;

/// <summary>
/// Cocina marca un ítem individual: Pendiente → En preparación → Listo (y permite deshacer
/// Listo → En preparación por si se tocó por error). El estado del pedido se recalcula solo.
/// </summary>
public record CambiarEstadoItemCocinaCommand(int PedidoId, int DetallePedidoId, EstadoDetallePedido Estado)
    : IRequest<PedidoCocinaDto>;

public class CambiarEstadoItemCocinaCommandHandler : IRequestHandler<CambiarEstadoItemCocinaCommand, PedidoCocinaDto>
{
    private static readonly Dictionary<EstadoDetallePedido, EstadoDetallePedido[]> Transiciones = new()
    {
        [EstadoDetallePedido.Pendiente] = new[] { EstadoDetallePedido.EnPreparacion, EstadoDetallePedido.Listo },
        [EstadoDetallePedido.EnPreparacion] = new[] { EstadoDetallePedido.Listo, EstadoDetallePedido.Pendiente },
        [EstadoDetallePedido.Listo] = new[] { EstadoDetallePedido.EnPreparacion },
    };

    private readonly IApplicationDbContext _context;
    private readonly IPedidosHubService _hub;

    public CambiarEstadoItemCocinaCommandHandler(IApplicationDbContext context, IPedidosHubService hub)
    {
        _context = context;
        _hub = hub;
    }

    public async Task<PedidoCocinaDto> Handle(CambiarEstadoItemCocinaCommand request, CancellationToken cancellationToken)
    {
        // Bloquea el pedido: si el mozo agrega platos mientras cocina lo marca, no se pisan.
        await using var tx = await _context.IniciarTransaccionAsync(cancellationToken);
        await _context.BloquearPedidoAsync(request.PedidoId, cancellationToken);

        var pedido = await _context.Pedidos
            .Include(p => p.Mesa).ThenInclude(m => m.Salon)
            .Include(p => p.Usuario)
            .Include(p => p.DetallesPedidos)
                .ThenInclude(d => d.Producto)
                    .ThenInclude(pr => pr.Categoria)
            .FirstOrDefaultAsync(p => p.PedidoId == request.PedidoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Pedido {request.PedidoId} no encontrado");

        if (pedido.EstadoPedido is EstadoPedido.Entregado or EstadoPedido.Cancelado)
            throw new InvalidOperationException($"El pedido ya está {pedido.EstadoPedido} y no puede modificarse en cocina");

        var detalle = pedido.DetallesPedidos.FirstOrDefault(d => d.DetallePedidoId == request.DetallePedidoId)
            ?? throw new KeyNotFoundException($"Ítem {request.DetallePedidoId} no pertenece al pedido {request.PedidoId}");

        var anterior = detalle.EstadoDetallePedido;
        if (anterior == request.Estado)
            return GetPedidosCocinaQueryHandler.Map(pedido);

        if (!Transiciones.TryGetValue(anterior, out var permitidas) || !permitidas.Contains(request.Estado))
            throw new InvalidOperationException($"No se puede pasar el ítem de {anterior} a {request.Estado}");

        detalle.EstadoDetallePedido = request.Estado;

        var estadoPedidoAnterior = pedido.EstadoPedido;
        pedido.EstadoPedido = CocinaEstadoHelper.CalcularEstadoPedido(pedido);

        await _context.SaveChangesAsync(cancellationToken);
        await tx.ConfirmarAsync(cancellationToken);

        var mesa = pedido.Mesa?.NumeroMesa ?? string.Empty;

        await _hub.NotificarDetalleEstadoCambiado(new DetalleEstadoCambiadoEvent
        {
            PedidoId = pedido.PedidoId,
            DetallePedidoId = detalle.DetallePedidoId,
            MesaNumero = mesa,
            ProductoNombre = detalle.Producto?.Nombre ?? string.Empty,
            EstadoAnterior = anterior.ToString(),
            EstadoNuevo = detalle.EstadoDetallePedido.ToString(),
            EstadoPedido = pedido.EstadoPedido.ToString()
        });

        if (estadoPedidoAnterior != pedido.EstadoPedido)
        {
            await _hub.NotificarPedidoEstadoCambiado(new PedidoEstadoCambiadoEvent
            {
                PedidoId = pedido.PedidoId,
                MesaNumero = mesa,
                SalonNombre = pedido.Mesa?.Salon?.Nombre ?? string.Empty,
                EstadoAnterior = estadoPedidoAnterior.ToString(),
                EstadoNuevo = pedido.EstadoPedido.ToString(),
                ActualizadoEn = DateTime.UtcNow
            });
        }

        return GetPedidosCocinaQueryHandler.Map(pedido);
    }
}
