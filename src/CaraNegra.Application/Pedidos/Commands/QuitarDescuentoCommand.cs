using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Pagos.DTOs;
using CaraNegra.Application.Pedidos.DTOs;
using CaraNegra.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Pedidos.Commands;

/// <summary>Quita el descuento aplicado a un pedido (Fase 7), si aún no tiene pagos registrados.</summary>
public record QuitarDescuentoCommand(int PedidoId) : IRequest<PedidoDto>;

public class QuitarDescuentoCommandHandler : IRequestHandler<QuitarDescuentoCommand, PedidoDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IPedidosHubService _hub;

    public QuitarDescuentoCommandHandler(IApplicationDbContext context, IPedidosHubService hub)
    {
        _context = context;
        _hub = hub;
    }

    public async Task<PedidoDto> Handle(QuitarDescuentoCommand request, CancellationToken cancellationToken)
    {
        var pedido = await _context.Pedidos
            .Include(p => p.Mesa)
            .Include(p => p.Usuario)
            .Include(p => p.DetallesPedidos)
                .ThenInclude(d => d.Producto)
            .Include(p => p.Pagos)
                .ThenInclude(p => p.MetodoPago)
            .Include(p => p.DetallesDescuentos)
                .ThenInclude(dd => dd.Descuento)
            .FirstOrDefaultAsync(p => p.PedidoId == request.PedidoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Pedido {request.PedidoId} no encontrado");

        if (pedido.Pagos.Any(p => !p.EstaAnulado))
        {
            throw new InvalidOperationException("No se puede quitar el descuento de un pedido que ya tiene pagos registrados");
        }

        if (!pedido.DetallesDescuentos.Any())
        {
            throw new InvalidOperationException("Este pedido no tiene ningún descuento aplicado");
        }

        foreach (var detalle in pedido.DetallesDescuentos.ToList())
        {
            _context.DetallesDescuento.Remove(detalle);
        }
        pedido.DetallesDescuentos.Clear();
        pedido.Total = pedido.SubTotal;

        await _context.SaveChangesAsync(cancellationToken);

        await _hub.NotificarPedidoActualizado(new PedidoActualizadoEvent
        {
            PedidoId = pedido.PedidoId,
            MesaNumero = pedido.Mesa?.NumeroMesa ?? string.Empty,
            SubTotal = pedido.SubTotal,
            Total = pedido.Total
        });

        return MapToDto(pedido);
    }

    private PedidoDto MapToDto(Pedido pedido)
    {
        return new PedidoDto
        {
            PedidoId = pedido.PedidoId,
            MesaId = pedido.MesaId,
            MesaNumero = pedido.Mesa?.NumeroMesa ?? string.Empty,
            UsuarioId = pedido.UsuarioId,
            UsuarioNombre = pedido.Usuario?.NombreCompleto ?? string.Empty,
            SubTotal = pedido.SubTotal,
            Total = pedido.Total,
            EstadoPedido = pedido.EstadoPedido,
            CreadoEn = pedido.CreadoEn,
            Descuento = null, // el descuento se acaba de quitar
            Detalles = pedido.DetallesPedidos.Select(d => new PedidoDetalleDto
            {
                DetallePedidoId = d.DetallePedidoId,
                ProductoId = d.ProductoId,
                ProductoNombre = d.Producto?.Nombre ?? string.Empty,
                Cantidad = d.Cantidad,
                Monto = d.Monto,
                Notas = d.Notas,
                EstadoDetallePedido = d.EstadoDetallePedido
            }).ToList(),
            Pagos = pedido.Pagos.Select(p => new PagoDto
            {
                PagoId = p.PagoId,
                PedidoId = p.PedidoId,
                MesaNumero = pedido.Mesa?.NumeroMesa ?? string.Empty,
                Monto = p.Monto,
                MetodoPagoId = p.MetodoPagoId,
                MetodoPagoNombre = p.MetodoPago?.Nombre ?? string.Empty,
                Referencia = p.Referencia,
                EstaAnulado = p.EstaAnulado,
                MotivoAnulacion = p.MotivoAnulacion,
                AnuladoEn = p.AnuladoEn,
                AnuladoPorUsuarioId = p.AnuladoPorUsuarioId,
                CreadoEn = p.CreadoEn
            }).ToList()
        };
    }
}
