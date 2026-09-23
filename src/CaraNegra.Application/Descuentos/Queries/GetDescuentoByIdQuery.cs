using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Descuentos.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Descuentos.Queries;

public record GetDescuentoByIdQuery(int DescuentoId) : IRequest<DescuentoDto>;

public class GetDescuentoByIdQueryHandler : IRequestHandler<GetDescuentoByIdQuery, DescuentoDto>
{
    private readonly IApplicationDbContext _context;

    public GetDescuentoByIdQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<DescuentoDto> Handle(GetDescuentoByIdQuery request, CancellationToken cancellationToken)
    {
        var descuento = await _context.Descuentos
            .FirstOrDefaultAsync(d => d.Descuentoid == request.DescuentoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Descuento {request.DescuentoId} no encontrado");

        return new DescuentoDto
        {
            DescuentoId = descuento.Descuentoid,
            Nombre = descuento.Nombre,
            EsPorcentaje = descuento.EsPorcentaje,
            Valor = descuento.Valor,
            EstaActivo = descuento.EstaActivo,
            FechaInicio = descuento.FechaInicio,
            FechaFin = descuento.FechaFin,
            CreadoEn = descuento.CreadoEn
        };
    }
}
