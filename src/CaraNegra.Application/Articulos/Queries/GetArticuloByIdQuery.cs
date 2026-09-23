using CaraNegra.Application.Articulos.DTOs;
using CaraNegra.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Articulos.Queries;

public record GetArticuloByIdQuery(int ArticuloId) : IRequest<ArticuloDto>;

public class GetArticuloByIdQueryHandler : IRequestHandler<GetArticuloByIdQuery, ArticuloDto>
{
    private readonly IApplicationDbContext _context;

    public GetArticuloByIdQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<ArticuloDto> Handle(GetArticuloByIdQuery request, CancellationToken cancellationToken)
    {
        var articulo = await _context.Articulos
            .Include(a => a.Categoria)
            .FirstOrDefaultAsync(a => a.ArticuloId == request.ArticuloId, cancellationToken)
            ?? throw new KeyNotFoundException($"Artículo {request.ArticuloId} no encontrado.");

        return new ArticuloDto
        {
            ArticuloId = articulo.ArticuloId,
            Nombre = articulo.Nombre,
            Descripcion = articulo.Descripcion,
            Precio = articulo.Precio,
            Stock = articulo.Stock,
            Activo = articulo.Activo,
            Tipo = articulo.Tipo,
            CategoriaId = articulo.CategoriaId,
            CategoriaNombre = articulo.Categoria.Nombre,
            CreadoEn = articulo.CreadoEn
        };
    }
}
