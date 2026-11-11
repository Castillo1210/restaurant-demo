using CaraNegra.Application.Jornadas;
using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Comprobantes;
using CaraNegra.Application.Mesas;
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

        // Bloqueo del pedido del pago (doble toque en "Anular" o cobro simultáneo).
        await using var tx = await _context.IniciarTransaccionAsync(cancellationToken);
        var pedidoDelPago = await _context.Pagos.AsNoTracking()
            .Where(p => p.PagoId == request.PagoId)
            .Select(p => (int?)p.PedidoId)
            .FirstOrDefaultAsync(cancellationToken);
        if (pedidoDelPago is int idPedido)
        {
            await _context.BloquearJornadaAbiertaAsync(exclusivo: false, cancellationToken);
            await _context.BloquearPedidoAsync(idPedido, cancellationToken);
        }

        var pago = await _context.Pagos
            .Include(p => p.MetodoPago)
            .Include(p => p.Pedido)
                .ThenInclude(p => p.Mesa).ThenInclude(m => m.Salon)
            .FirstOrDefaultAsync(p => p.PagoId == request.PagoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Pago {request.PagoId} no encontrado");

        if (pago.EstaAnulado)
            throw new InvalidOperationException("Este pago ya fue anulado anteriormente");

        // Una jornada cerrada (con su arqueo) es de solo lectura.
        if (pago.JornadaId is int jornadaId)
        {
            var cerrada = await _context.Jornadas.AnyAsync(
                j => j.JornadaId == jornadaId && j.Estado == EstadoJornada.Cerrada, cancellationToken);
            if (cerrada)
                throw new InvalidOperationException("El pago es de una jornada ya cerrada (con arqueo): no se puede anular");
        }

        var pedido = pago.Pedido;

        // No permitir anular si el pedido está cancelado
        if (pedido.EstadoPedido == EstadoPedido.Cancelado)
            throw new InvalidOperationException("No se puede anular un pago de un pedido cancelado");

        // Con boleta o factura válida, anular el pago dejaría en SUNAT un comprobante de una venta que
        // ya no existe (o distinto de lo que se termine cobrando). Mientras no haya nota de crédito,
        // no se permite. Un comprobante rechazado o con error no cuenta.
        var comprobantes = await _context.Comprobantes.AsNoTracking()
            .Where(c => c.PedidoId == pedido.PedidoId)
            .Select(c => new { c.Estado, c.Serie, c.Correlativo })
            .ToListAsync(cancellationToken);
        var valido = comprobantes.FirstOrDefault(c => !ComprobanteEnvio.NoEsValido(c.Estado));
        if (valido is not null)
            throw new InvalidOperationException(
                $"Este pedido ya tiene el comprobante {ComprobanteMapper.FormatearNumero(valido.Serie, valido.Correlativo)}: " +
                "no se puede anular el pago (haría falta una nota de crédito).");

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
            mesaReocupada = MesaOcupacion.Ocupar(mesa);
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
        await tx.ConfirmarAsync(cancellationToken);

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
                SalonNombre = pago.Pedido?.Mesa?.Salon?.Nombre ?? string.Empty,
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