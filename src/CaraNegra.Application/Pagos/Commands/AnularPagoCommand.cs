using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Pagos.DTOs;
using CaraNegra.Domain.Entities;
using CaraNegra.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Pagos.Commands;

public record AnularPagoCommand(int PagoId, string Motivo, int UsuarioId) : IRequest<PagoDto>;

public class AnularPagoCommandHandler : IRequestHandler<AnularPagoCommand, PagoDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IPedidosHubService _hub;

    public AnularPagoCommandHandler(IApplicationDbContext context, IPedidosHubService hub)
    {
        _context = context;
        _hub = hub;
    }

    public async Task<PagoDto> Handle(AnularPagoCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Motivo))
            throw new InvalidOperationException("Debe indicar un motivo para anular el pago");

        var pago = await _context.Pagos
            .Include(p => p.MetodoPago)
            .Include(p => p.Pedido)
                .ThenInclude(p => p.Mesa)
            .FirstOrDefaultAsync(p => p.PagoId == request.PagoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Pago {request.PagoId} no encontrado");

        if (pago.EstaAnulado)
            throw new InvalidOperationException("Este pago ya fue anulado anteriormente");

        var pedido = pago.Pedido;

        // No permitir anular si el pedido está cancelado
        if (pedido.EstadoPedido == EstadoPedido.Cancelado)
            throw new InvalidOperationException("No se puede anular un pago de un pedido cancelado");

        var pedidoId = pedido.PedidoId;
        var mesaId = pedido.MesaId;
        var eraEntregado = pedido.EstadoPedido == EstadoPedido.Entregado;

        // Anulación con auditoría: el registro NUNCA se borra físicamente, para conservar
        // el rastro de qué se cobró, quién lo anuló y por qué (control interno de caja).
        pago.EstaAnulado = true;
        pago.MotivoAnulacion = request.Motivo;
        pago.AnuladoEn = DateTime.UtcNow;
        pago.AnuladoPorUsuarioId = request.UsuarioId;

        await _context.SaveChangesAsync(cancellationToken);

        // Recalcular estado del pedido excluyendo pagos anulados
        var totalPagado = await _context.Pagos
            .Where(p => p.PedidoId == pedidoId && !p.EstaAnulado)
            .SumAsync(p => p.Monto, cancellationToken);

        var estadoAnteriorPedido = pedido.EstadoPedido;
        var mesaReocupada = false;

        if (eraEntregado && totalPagado < pedido.Total - 0.01m)
        {
            // Volver a estado Listo para cobro
            pedido.EstadoPedido = EstadoPedido.Listo;

            // Ocupar mesa nuevamente
            var mesa = await _context.Mesas.FindAsync(new object[] { mesaId }, cancellationToken);
            if (mesa != null && mesa.Estado != EstadoMesa.Ocupada)
            {
                mesa.Estado = EstadoMesa.Ocupada;
                mesaReocupada = true;
            }
        }
        else if (totalPagado == 0)
        {
            // Sin pagos activos, volver a Pendiente si estaba en Listo
            if (pedido.EstadoPedido == EstadoPedido.Listo)
            {
                pedido.EstadoPedido = EstadoPedido.Pendiente;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _hub.NotificarPagoAnulado(new PagoAnuladoEvent
        {
            PedidoId = pedidoId,
            MesaNumero = pago.Pedido?.Mesa?.NumeroMesa ?? string.Empty,
            MontoAnulado = pago.Monto,
            NuevoEstadoPedido = pedido.EstadoPedido.ToString()
        });

        if (pedido.EstadoPedido != estadoAnteriorPedido)
        {
            await _hub.NotificarPedidoEstadoCambiado(new PedidoEstadoCambiadoEvent
            {
                PedidoId = pedidoId,
                MesaNumero = pago.Pedido?.Mesa?.NumeroMesa ?? string.Empty,
                EstadoAnterior = estadoAnteriorPedido.ToString(),
                EstadoNuevo = pedido.EstadoPedido.ToString(),
                ActualizadoEn = DateTime.UtcNow
            });
        }

        if (mesaReocupada)
        {
            await _hub.NotificarMesaEstadoCambiado(new MesaEstadoCambiadoEvent
            {
                MesaId = mesaId,
                NumeroMesa = pago.Pedido?.Mesa?.NumeroMesa ?? string.Empty,
                EstadoAnterior = EstadoMesa.Disponible.ToString(),
                EstadoNuevo = EstadoMesa.Ocupada.ToString()
            });
        }

        return MapToDto(pago);
    }

    private PagoDto MapToDto(Pago pago)
    {
        return new PagoDto
        {
            PagoId = pago.PagoId,
            PedidoId = pago.PedidoId,
            MesaNumero = pago.Pedido?.Mesa?.NumeroMesa ?? string.Empty,
            Monto = pago.Monto,
            MetodoPagoId = pago.MetodoPagoId,
            MetodoPagoNombre = pago.MetodoPago?.Nombre ?? string.Empty,
            Referencia = pago.Referencia,
            EstaAnulado = pago.EstaAnulado,
            MotivoAnulacion = pago.MotivoAnulacion,
            AnuladoEn = pago.AnuladoEn,
            AnuladoPorUsuarioId = pago.AnuladoPorUsuarioId,
            CreadoEn = pago.CreadoEn
        };
    }
}