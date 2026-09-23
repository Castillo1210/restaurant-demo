using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Cremas.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Cremas.Queries;

public record GetAllCremasQuery(bool SoloActivas = true) : IRequest<List<CremaDto>>;

public class GetAllCremasQueryHandler : IRequestHandler<GetAllCremasQuery, List<CremaDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAllCremasQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<CremaDto>> Handle(GetAllCremasQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Cremas.AsQueryable();

        if (request.SoloActivas)
        {
            query = query.Where(c => c.EstaActivo);
        }

        return await query
            .OrderBy(c => c.Orden)
            .ThenBy(c => c.Nombre)
            .Select(c => new CremaDto
            {
                CremaId = c.CremaId,
                Nombre = c.Nombre,
                Orden = c.Orden,
                EstaActivo = c.EstaActivo,
                CreadoEn = c.CreadoEn
            }).ToListAsync(cancellationToken);
    }
}
