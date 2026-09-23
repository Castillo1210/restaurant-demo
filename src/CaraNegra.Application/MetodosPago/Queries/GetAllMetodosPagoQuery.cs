using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.MetodosPago.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.MetodosPago.Queries;

public record GetAllMetodosPagoQuery(bool SoloActivos = true) : IRequest<List<MetodoPagoDto>>;

public class GetAllMetodosPagoQueryHandler : IRequestHandler<GetAllMetodosPagoQuery, List<MetodoPagoDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAllMetodosPagoQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<MetodoPagoDto>> Handle(GetAllMetodosPagoQuery request, CancellationToken cancellationToken)
    {
        var query = _context.MetodosPago.AsQueryable();

        if (request.SoloActivos)
        {
            query = query.Where(m => m.EstaActivo);
        }

        return await query
            .OrderBy(m => m.Nombre)
            .Select(m => new MetodoPagoDto
            {
                MetodoPagoId = m.MetodoPagoId,
                Nombre = m.Nombre,
                EstaActivo = m.EstaActivo,
                CreadoEn = m.CreadoEn
            }).ToListAsync(cancellationToken);
    }
}
