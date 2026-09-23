using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.MetodosPago.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.MetodosPago.Queries;

public record GetMetodoPagoByIdQuery(int MetodoPagoId) : IRequest<MetodoPagoDto>;

public class GetMetodoPagoByIdQueryHandler : IRequestHandler<GetMetodoPagoByIdQuery, MetodoPagoDto>
{
    private readonly IApplicationDbContext _context;

    public GetMetodoPagoByIdQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<MetodoPagoDto> Handle(GetMetodoPagoByIdQuery request, CancellationToken cancellationToken)
    {
        var metodoPago = await _context.MetodosPago
            .FirstOrDefaultAsync(m => m.MetodoPagoId == request.MetodoPagoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Método de pago {request.MetodoPagoId} no encontrado.");

        return new MetodoPagoDto
        {
            MetodoPagoId = metodoPago.MetodoPagoId,
            Nombre = metodoPago.Nombre,
            EstaActivo = metodoPago.EstaActivo,
            CreadoEn = metodoPago.CreadoEn
        };
    }
}
