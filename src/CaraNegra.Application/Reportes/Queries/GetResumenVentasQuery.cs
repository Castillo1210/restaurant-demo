using CaraNegra.Application.Common;
using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Reportes.DTOs;
using CaraNegra.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Reportes.Queries;

public record GetResumenVentasQuery(DateTime FechaDesde, DateTime FechaHasta) : IRequest<ResumenVentasDto>;

public class GetResumenVentasQueryHandler : IRequestHandler<GetResumenVentasQuery, ResumenVentasDto>
{
    private readonly IApplicationDbContext _context;

    public GetResumenVentasQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<ResumenVentasDto> Handle(GetResumenVentasQuery request, CancellationToken cancellationToken)
    {
        if (request.FechaDesde > request.FechaHasta)
        {
            throw new InvalidOperationException("La fecha de inicio no puede ser posterior a la fecha de fin.");
        }

        // CreadoEn se guarda en UTC; fechaDesde/fechaHasta representan el calendario local de
        // Lima (ver PeruDateRangeHelper) — hay que convertir antes de comparar, si no, los
        // pedidos de la tarde/noche caen ya en el día UTC siguiente y no aparecen en "hoy".
        var fechaDesde = PeruDateRangeHelper.InicioDelDiaUtc(request.FechaDesde);
        var fechaHasta = PeruDateRangeHelper.FinDelDiaUtc(request.FechaHasta);

        var pedidosEnRango = _context.Pedidos
            .Where(p => p.CreadoEn >= fechaDesde && p.CreadoEn <= fechaHasta);

        var cantidadPedidos = await pedidosEnRango.CountAsync(cancellationToken);
        var cantidadCancelados = await pedidosEnRango.CountAsync(p => p.EstadoPedido == EstadoPedido.Cancelado, cancellationToken);

        var pagosEnRango = _context.Pagos
            .Include(p => p.MetodoPago)
            .Where(p => !p.EstaAnulado && p.CreadoEn >= fechaDesde && p.CreadoEn <= fechaHasta);

        var totalVentas = await pagosEnRango.SumAsync(p => (decimal?)p.Monto, cancellationToken) ?? 0m;
        var cantidadPedidosPagados = await pagosEnRango.Select(p => p.PedidoId).Distinct().CountAsync(cancellationToken);

        var ventasPorMetodoPago = await pagosEnRango
            .GroupBy(p => p.MetodoPago.Nombre)
            .Select(g => new VentaPorMetodoPagoDto
            {
                MetodoPagoNombre = g.Key,
                Total = g.Sum(p => p.Monto),
                CantidadPagos = g.Count()
            })
            .OrderByDescending(v => v.Total)
            .ToListAsync(cancellationToken);

        // Total de descuentos otorgados (Fase 7): se excluyen los cancelados porque no
        // representan una venta real, solo un pedido que nunca se concretó.
        var totalDescuentos = await pedidosEnRango
            .Where(p => p.EstadoPedido != EstadoPedido.Cancelado)
            .SumAsync(p => p.SubTotal - p.Total, cancellationToken);

        return new ResumenVentasDto
        {
            FechaDesde = request.FechaDesde.Date,
            FechaHasta = request.FechaHasta.Date,
            TotalVentas = totalVentas,
            CantidadPedidos = cantidadPedidos,
            CantidadPedidosCancelados = cantidadCancelados,
            CantidadPedidosPagados = cantidadPedidosPagados,
            TicketPromedio = cantidadPedidosPagados > 0 ? totalVentas / cantidadPedidosPagados : 0m,
            TotalDescuentos = totalDescuentos,
            VentasPorMetodoPago = ventasPorMetodoPago
        };
    }
}
