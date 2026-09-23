using CaraNegra.Application.Categorias.DTOs;
using CaraNegra.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Categorias.Queries;

public record GetAllCategoriasQuery(bool SoloActivas = true) : IRequest<List<CategoriaDto>>;

public class GetAllCategoriasQueryHandler : IRequestHandler<GetAllCategoriasQuery, List<CategoriaDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAllCategoriasQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<CategoriaDto>> Handle(GetAllCategoriasQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Categorias.AsQueryable();

        if (request.SoloActivas)
        {
            query = query.Where(c => c.EstaActivo);
        }

        return await query.Select(c => new CategoriaDto
        {
            CategoriaId = c.CategoriaId,
            Nombre = c.Nombre,
            Descripcion = c.Descripcion,
            EstaActivo = c.EstaActivo,
            CreadoEn = c.CreadoEn
        }).ToListAsync(cancellationToken);
    }
}