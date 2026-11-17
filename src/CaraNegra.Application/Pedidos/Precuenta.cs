using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Pedidos;

/// <summary>
/// Registra que se imprimió la nota de pedido (precuenta): el cliente pidió la cuenta. Caja muestra
/// esos pedidos primero. No cambia montos ni el estado del pedido.
/// </summary>
public record MarcarPrecuentaCommand(int PedidoId) : IRequest;

public class MarcarPrecuentaCommandHandler : IRequestHandler<MarcarPrecuentaCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IPedidosHubService _hub;

    public MarcarPrecuentaCommandHandler(IApplicationDbContext context, IPedidosHubService hub)
    {
        _context = context;
        _hub = hub;
    }

    public async Task Handle(MarcarPrecuentaCommand request, CancellationToken cancellationToken)
    {
        var pedido = await _context.Pedidos
            .Include(p => p.Mesa)
            .FirstOrDefaultAsync(p => p.PedidoId == request.PedidoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Pedido {request.PedidoId} no encontrado");

        // Cobrado o anulado: ya no importa para caja.
        if (pedido.EstadoPedido is EstadoPedido.Entregado or EstadoPedido.Cancelado) return;

        pedido.PrecuentaImpresaEn = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        // Las otras cajas reordenan su lista.
        await _hub.NotificarPedidoActualizado(new PedidoActualizadoEvent
        {
            PedidoId = pedido.PedidoId,
            MesaNumero = pedido.Mesa?.NumeroMesa ?? string.Empty,
            SubTotal = pedido.SubTotal,
            Total = pedido.Total
        });
    }
}
