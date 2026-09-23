using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Pagos.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Pagos.Queries;

public record GetPagoByIdQuery(int PagoId) : IRequest<PagoDto>;

public class GetPagoByIdQueryHandler : IRequestHandler<GetPagoByIdQuery, PagoDto>
{
    private readonly IApplicationDbContext _context;

    public GetPagoByIdQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<PagoDto> Handle(GetPagoByIdQuery request, CancellationToken cancellationToken)
    {
        var pago = await _context.Pagos
            .Include(p => p.MetodoPago)
            .Include(p => p.Pedido)
                .ThenInclude(p => p.Mesa)
            .FirstOrDefaultAsync(p => p.PagoId == request.PagoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Pago {request.PagoId} no encontrado");

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