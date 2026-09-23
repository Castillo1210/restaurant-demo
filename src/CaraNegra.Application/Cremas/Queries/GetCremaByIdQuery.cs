using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Cremas.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Cremas.Queries;

public record GetCremaByIdQuery(int CremaId) : IRequest<CremaDto>;

public class GetCremaByIdQueryHandler : IRequestHandler<GetCremaByIdQuery, CremaDto>
{
    private readonly IApplicationDbContext _context;

    public GetCremaByIdQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<CremaDto> Handle(GetCremaByIdQuery request, CancellationToken cancellationToken)
    {
        var crema = await _context.Cremas
            .FirstOrDefaultAsync(c => c.CremaId == request.CremaId, cancellationToken)
            ?? throw new KeyNotFoundException($"Crema {request.CremaId} no encontrada.");

        return new CremaDto
        {
            CremaId = crema.CremaId,
            Nombre = crema.Nombre,
            Orden = crema.Orden,
            EstaActivo = crema.EstaActivo,
            CreadoEn = crema.CreadoEn
        };
    }
}
