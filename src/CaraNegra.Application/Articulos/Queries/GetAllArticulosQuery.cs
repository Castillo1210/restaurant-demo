using CaraNegra.Application.Articulos.DTOs;
using CaraNegra.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Articulos.Queries;

public record GetAllArticulosQuery(bool SoloActivos = true) : IRequest<List<ArticuloDto>>;

public class GetAllArticulosQueryHandler : IRequestHandler<GetAllArticulosQuery, List<ArticuloDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAllArticulosQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<ArticuloDto>> Handle(GetAllArticulosQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Articulos
            .Include(a => a.Categoria)
            .AsQueryable();

        if (request.SoloActivos)
        {
            query = query.Where(a => a.Activo);
        }

        return await query
            .OrderBy(a => a.Nombre)
            .Select(a => new ArticuloDto
            {
                ArticuloId = a.ArticuloId,
                Nombre = a.Nombre,
                Descripcion = a.Descripcion,
                Precio = a.Precio,
                Stock = a.Stock,
                Activo = a.Activo,
                Tipo = a.Tipo,
                CategoriaId = a.CategoriaId,
                CategoriaNombre = a.Categoria.Nombre,
                CreadoEn = a.CreadoEn
            }).ToListAsync(cancellationToken);
    }
}
