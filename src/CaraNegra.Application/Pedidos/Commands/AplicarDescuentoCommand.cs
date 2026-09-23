using CaraNegra.Application.Common;
using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Pagos.DTOs;
using CaraNegra.Application.Pedidos.DTOs;
using CaraNegra.Domain.Entities;
using CaraNegra.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Pedidos.Commands;

/// <summary>
/// Aplica un descuento (Fase 7) a un pedido. Solo se permite antes de registrar cualquier pago
/// sobre ese pedido — así se evita tener que recalcular pagos parciales ya hechos contra un
/// Total que acaba de cambiar. Solo se permite un descuento activo por pedido a la vez.
/// </summary>
public record AplicarDescuentoCommand(int PedidoId, int DescuentoId) : IRequest<PedidoDto>;

public class AplicarDescuentoCommandHandler : IRequestHandler<AplicarDescuentoCommand, PedidoDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IPedidosHubService _hub;

    public AplicarDescuentoCommandHandler(IApplicationDbContext context, IPedidosHubService hub)
    {
        _context = context;
        _hub = hub;
    }

    public async Task<PedidoDto> Handle(AplicarDescuentoCommand request, CancellationToken cancellationToken)
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

        if (pedido.EstadoPedido == EstadoPedido.Cancelado || pedido.EstadoPedido == EstadoPedido.Entregado)
        {
            throw new InvalidOperationException("No se puede aplicar un descuento a un pedido cancelado o ya entregado");
        }

        if (pedido.Pagos.Any(p => !p.EstaAnulado))
        {
            throw new InvalidOperationException(
                "No se puede aplicar un descuento a un pedido que ya tiene pagos registrados. Anula los pagos primero si necesitas corregirlo.");
        }

        if (pedido.DetallesDescuentos.Any())
        {
            throw new InvalidOperationException("Este pedido ya tiene un descuento aplicado. Quítalo primero para aplicar otro.");
        }

        var descuento = await _context.Descuentos
            .FirstOrDefaultAsync(d => d.Descuentoid == request.DescuentoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Descuento {request.DescuentoId} no encontrado");

        if (!descuento.EstaActivo)
        {
            throw new InvalidOperationException("Este descuento está desactivado");
        }

        // Vigencia comparada contra la fecha calendario de hoy en Lima (UTC-5), igual que el
        // resto del sistema (ver PeruDateRangeHelper) — evita que un descuento con FechaFin
        // "hoy" se dé por vencido horas antes por la diferencia de huso horario con UTC.
        var hoyEnLima = DateTime.UtcNow.AddHours(-5).Date;
        if (descuento.FechaInicio.HasValue && descuento.FechaInicio.Value.Date > hoyEnLima)
        {
            throw new InvalidOperationException("Este descuento todavía no está vigente");
        }
        if (descuento.FechaFin.HasValue && descuento.FechaFin.Value.Date < hoyEnLima)
        {
            throw new InvalidOperationException("Este descuento ya venció");
        }

        _context.DetallesDescuento.Add(new DetalleDescuento
        {
            PedidoId = pedido.PedidoId,
            Descuentoid = descuento.Descuentoid
        });

        pedido.Total = DescuentoCalculator.CalcularTotal(pedido.SubTotal, descuento);

        await _context.SaveChangesAsync(cancellationToken);

        var pedidoActualizado = await _context.Pedidos
            .Include(p => p.Mesa)
            .Include(p => p.Usuario)
            .Include(p => p.DetallesPedidos)
                .ThenInclude(d => d.Producto)
            .Include(p => p.Pagos)
                .ThenInclude(p => p.MetodoPago)
            .Include(p => p.DetallesDescuentos)
                .ThenInclude(dd => dd.Descuento)
            .FirstAsync(p => p.PedidoId == request.PedidoId, cancellationToken);

        await _hub.NotificarPedidoActualizado(new PedidoActualizadoEvent
        {
            PedidoId = pedidoActualizado.PedidoId,
            MesaNumero = pedidoActualizado.Mesa?.NumeroMesa ?? string.Empty,
            SubTotal = pedidoActualizado.SubTotal,
            Total = pedidoActualizado.Total
        });

        return MapToDto(pedidoActualizado);
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
