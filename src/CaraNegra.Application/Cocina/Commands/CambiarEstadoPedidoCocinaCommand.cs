using CaraNegra.Application.Cocina.DTOs;
using CaraNegra.Application.Cocina.Queries;
using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Cocina.Commands;

/// <summary>
/// Botones grandes de la pantalla de cocina: "Empezar" (todo el pedido a En preparación) y
/// "Pedido listo" (todo a Listo). Mueve a la vez todos los ítems activos, así cocina no tiene
/// que tocar ítem por ítem cuando prepara el pedido completo de una vez.
/// </summary>
public record CambiarEstadoPedidoCocinaCommand(int PedidoId, EstadoPedido Estado) : IRequest<PedidoCocinaDto>;

public class CambiarEstadoPedidoCocinaCommandHandler : IRequestHandler<CambiarEstadoPedidoCocinaCommand, PedidoCocinaDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IPedidosHubService _hub;

    public CambiarEstadoPedidoCocinaCommandHandler(IApplicationDbContext context, IPedidosHubService hub)
    {
        _context = context;
        _hub = hub;
    }

    public async Task<PedidoCocinaDto> Handle(CambiarEstadoPedidoCocinaCommand request, CancellationToken cancellationToken)
    {
        if (request.Estado is not (EstadoPedido.EnPreparacion or EstadoPedido.Listo))
            throw new InvalidOperationException("Desde cocina solo se puede pasar un pedido a En preparación o Listo");

        var pedido = await _context.Pedidos
            .Include(p => p.Mesa)
            .Include(p => p.Usuario)
            .Include(p => p.DetallesPedidos)
                .ThenInclude(d => d.Producto)
                    .ThenInclude(pr => pr.Categoria)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.PedidoId == request.PedidoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Pedido {request.PedidoId} no encontrado");

        if (pedido.EstadoPedido is EstadoPedido.Entregado or EstadoPedido.Cancelado)
            throw new InvalidOperationException($"El pedido ya está {pedido.EstadoPedido} y no puede modificarse en cocina");

        var estadoAnterior = pedido.EstadoPedido;
        var destinoItem = request.Estado == EstadoPedido.Listo
            ? EstadoDetallePedido.Listo
            : EstadoDetallePedido.EnPreparacion;

        foreach (var d in pedido.DetallesPedidos)
        {
            if (d.EstadoDetallePedido is EstadoDetallePedido.Cancelado or EstadoDetallePedido.Entregado)
                continue;

            // "Empezar" no retrocede ítems que ya estaban listos.
            if (destinoItem == EstadoDetallePedido.EnPreparacion && d.EstadoDetallePedido == EstadoDetallePedido.Listo)
                continue;

            d.EstadoDetallePedido = destinoItem;
        }

        pedido.EstadoPedido = CocinaEstadoHelper.CalcularEstadoPedido(pedido);

        await _context.SaveChangesAsync(cancellationToken);

        if (estadoAnterior != pedido.EstadoPedido)
        {
            await _hub.NotificarPedidoEstadoCambiado(new PedidoEstadoCambiadoEvent
            {
                PedidoId = pedido.PedidoId,
                MesaNumero = pedido.Mesa?.NumeroMesa ?? string.Empty,
                EstadoAnterior = estadoAnterior.ToString(),
                EstadoNuevo = pedido.EstadoPedido.ToString(),
                ActualizadoEn = DateTime.UtcNow
            });
        }
        else
        {
            // Aunque el pedido no cambie de estado (p.ej. ya estaba En preparación), sus ítems sí:
            // se avisa para que las otras pantallas de cocina se refresquen.
            await _hub.NotificarPedidoActualizado(new PedidoActualizadoEvent
            {
                PedidoId = pedido.PedidoId,
                MesaNumero = pedido.Mesa?.NumeroMesa ?? string.Empty,
                SubTotal = pedido.SubTotal,
                Total = pedido.Total
            });
        }

        return GetPedidosCocinaQueryHandler.Map(pedido);
    }
}
