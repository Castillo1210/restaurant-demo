using CaraNegra.Application.Common;
using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Pagos.DTOs;
using CaraNegra.Application.Pagos.Queries;
using CaraNegra.Application.Pedidos.DTOs;
using CaraNegra.Domain.Entities;
using CaraNegra.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Pedidos.Queries;

public record GetAllPedidosQuery(
    int Page = 1,
    int PageSize = 20,
    EstadoPedido? Estado = null,
    int? MesaId = null,
    DateTime? FechaDesde = null,
    DateTime? FechaHasta = null) : IRequest<PagedResult<PedidoDto>>;

public class GetAllPedidosQueryHandler : IRequestHandler<GetAllPedidosQuery, PagedResult<PedidoDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAllPedidosQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<PagedResult<PedidoDto>> Handle(GetAllPedidosQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Pedidos
            .Include(p => p.Mesa)
            .Include(p => p.Usuario)
            .Include(p => p.DetallesPedidos)
                .ThenInclude(d => d.Producto)
            .Include(p => p.Pagos)
                .ThenInclude(p => p.MetodoPago)
            .Include(p => p.DetallesDescuentos)
                .ThenInclude(dd => dd.Descuento)
            .AsQueryable();

        if (request.Estado.HasValue)
        {
            query = query.Where(p => p.EstadoPedido == request.Estado.Value);
        }

        if (request.MesaId.HasValue)
        {
            query = query.Where(p => p.MesaId == request.MesaId.Value);
        }

        if (request.FechaDesde.HasValue)
        {
            // Ver PeruDateRangeHelper: CreadoEn está en UTC, fechaDesde representa el
            // calendario local de Lima (UTC-5).
            query = query.Where(p => p.CreadoEn >= PeruDateRangeHelper.InicioDelDiaUtc(request.FechaDesde.Value));
        }

        if (request.FechaHasta.HasValue)
        {
            var fechaHasta = PeruDateRangeHelper.FinDelDiaUtc(request.FechaHasta.Value);
            query = query.Where(p => p.CreadoEn <= fechaHasta);
        }

        var total = await query.CountAsync(cancellationToken);

        var pedidos = await query
            .OrderByDescending(p => p.CreadoEn)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var items = pedidos.Select(MapToDto).ToList();

        return new PagedResult<PedidoDto>
        {
            Items = items,
            TotalCount = total,
            Page = request.Page,
            PageSize = request.PageSize
        };
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
