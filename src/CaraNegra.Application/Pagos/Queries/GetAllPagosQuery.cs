using CaraNegra.Application.Common;
using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Pagos.DTOs;
using CaraNegra.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Pagos.Queries;

public record GetAllPagosQuery(
    int Page = 1, 
    int PageSize = 20, 
    DateTime? FechaDesde = null, 
    DateTime? FechaHasta = null, 
    int? MetodoPagoId = null) : IRequest<PagedResult<PagoDto>>;

public class GetAllPagosQueryHandler : IRequestHandler<GetAllPagosQuery, PagedResult<PagoDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAllPagosQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<PagedResult<PagoDto>> Handle(GetAllPagosQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Pagos
            .Include(p => p.MetodoPago)
            .Include(p => p.Pedido)
                .ThenInclude(p => p.Mesa)
            .AsQueryable();

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

        if (request.MetodoPagoId.HasValue)
        {
            query = query.Where(p => p.MetodoPagoId == request.MetodoPagoId.Value);
        }

        var total = await query.CountAsync(cancellationToken);

        var pagos = await query
            .OrderByDescending(p => p.CreadoEn)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var items = pagos.Select(MapToDto).ToList();

        return new PagedResult<PagoDto>
        {
            Items = items,
            TotalCount = total,
            Page = request.Page,
            PageSize = request.PageSize
        };
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

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}