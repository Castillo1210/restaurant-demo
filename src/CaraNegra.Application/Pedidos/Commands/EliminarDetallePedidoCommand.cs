using CaraNegra.Application.Common;
using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Pagos.DTOs;
using CaraNegra.Application.Pedidos.DTOs;
using CaraNegra.Domain.Entities;
using CaraNegra.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Pedidos.Commands;

public record EliminarDetallePedidoCommand(int PedidoId, int DetallePedidoId) : IRequest<PedidoDto>;

public class EliminarDetallePedidoCommandHandler : IRequestHandler<EliminarDetallePedidoCommand, PedidoDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IPedidosHubService _hub;

    public EliminarDetallePedidoCommandHandler(IApplicationDbContext context, IPedidosHubService hub)
    {
        _context = context;
        _hub = hub;
    }

    public async Task<PedidoDto> Handle(EliminarDetallePedidoCommand request, CancellationToken cancellationToken)
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

        if (pedido.EstadoPedido != EstadoPedido.Pendiente && pedido.EstadoPedido != EstadoPedido.EnPreparacion)
        {
            throw new InvalidOperationException("Solo se pueden quitar items de pedidos Pendientes o En Preparación");
        }

        var detalle = pedido.DetallesPedidos.FirstOrDefault(d => d.DetallePedidoId == request.DetallePedidoId)
            ?? throw new KeyNotFoundException($"El detalle {request.DetallePedidoId} no pertenece al pedido {request.PedidoId}");

        if (pedido.DetallesPedidos.Count <= 1)
        {
            throw new InvalidOperationException("El pedido debe tener al menos un item. Para quitar el último, cancele el pedido completo.");
        }

        pedido.SubTotal -= detalle.Monto * detalle.Cantidad;
        if (pedido.SubTotal < 0) pedido.SubTotal = 0;
        var descuentoActivo = pedido.DetallesDescuentos.FirstOrDefault()?.Descuento;
        pedido.Total = DescuentoCalculator.CalcularTotal(pedido.SubTotal, descuentoActivo);

        _context.DetallesPedido.Remove(detalle);
        pedido.DetallesPedidos.Remove(detalle);

        // Si el ítem quitado era el único que cocina tenía pendiente, el pedido puede quedar listo.
        var estadoPedidoAnterior = pedido.EstadoPedido;
        pedido.EstadoPedido = CaraNegra.Application.Cocina.CocinaEstadoHelper.CalcularEstadoPedido(pedido);

        await _context.SaveChangesAsync(cancellationToken);

        if (estadoPedidoAnterior != pedido.EstadoPedido)
        {
            await _hub.NotificarPedidoEstadoCambiado(new PedidoEstadoCambiadoEvent
            {
                PedidoId = pedido.PedidoId,
                MesaNumero = pedido.Mesa?.NumeroMesa ?? string.Empty,
                EstadoAnterior = estadoPedidoAnterior.ToString(),
                EstadoNuevo = pedido.EstadoPedido.ToString(),
                ActualizadoEn = DateTime.UtcNow
            });
        }

        await _hub.NotificarPedidoActualizado(new PedidoActualizadoEvent
        {
            PedidoId = pedido.PedidoId,
            MesaNumero = pedido.Mesa.NumeroMesa,
            SubTotal = pedido.SubTotal,
            Total = pedido.Total
        });

        return MapToDto(pedido);
    }

    private static DescuentoAplicadoDto? MapDescuento(Pedido pedido)
    {
        var detalle = pedido.DetallesDescuentos.FirstOrDefault();
        if (detalle?.Descuento == null) return null;
        return new DescuentoAplicadoDto
        {
            DescuentoId = detalle.Descuentoid,
            Nombre = detalle.Descuento.Nombre,
            EsPorcentaje = detalle.Descuento.EsPorcentaje,
            Valor = detalle.Descuento.Valor,
            MontoDescuento = pedido.SubTotal - pedido.Total
        };
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
            Descuento = MapDescuento(pedido),
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
