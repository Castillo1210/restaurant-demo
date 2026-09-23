using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Pagos.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Pagos.Queries;

public record GetPagosByPedidoQuery(int PedidoId) : IRequest<List<PagoDto>>;

public class GetPagosByPedidoQueryHandler : IRequestHandler<GetPagosByPedidoQuery, List<PagoDto>>
{
    private readonly IApplicationDbContext _context;

    public GetPagosByPedidoQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<PagoDto>> Handle(GetPagosByPedidoQuery request, CancellationToken cancellationToken)
    {
        var pagos = await _context.Pagos
            .Include(p => p.MetodoPago)
            .Where(p => p.PedidoId == request.PedidoId)
            .OrderBy(p => p.CreadoEn)
            .ToListAsync(cancellationToken);

        return pagos.Select(p => new PagoDto
        {
            PagoId = p.PagoId,
            PedidoId = p.PedidoId,
            MesaNumero = p.Pedido?.Mesa?.NumeroMesa ?? string.Empty,
            Monto = p.Monto,
            MetodoPagoId = p.MetodoPagoId,
            MetodoPagoNombre = p.MetodoPago?.Nombre ?? string.Empty,
            Referencia = p.Referencia,
            EstaAnulado = p.EstaAnulado,
            MotivoAnulacion = p.MotivoAnulacion,
            AnuladoEn = p.AnuladoEn,
            AnuladoPorUsuarioId = p.AnuladoPorUsuarioId,
            CreadoEn = p.CreadoEn
        }).ToList();
    }
}