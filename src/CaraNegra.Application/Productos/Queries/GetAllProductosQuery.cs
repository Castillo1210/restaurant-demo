using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Productos.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Productos.Queries;

public record GetAllProductosQuery(bool SoloDisponibles = true, int? CategoriaId = null) : IRequest<List<ProductoDto>>;

public class GetAllProductosQueryHandler : IRequestHandler<GetAllProductosQuery, List<ProductoDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAllProductosQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<ProductoDto>> Handle(GetAllProductosQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Productos
            .Include(p => p.Categoria)
            .AsQueryable();

        if (request.SoloDisponibles)
        {
            query = query.Where(p => p.EstaDisponible);
        }

        if (request.CategoriaId.HasValue)
        {
            query = query.Where(p => p.CategoriaId == request.CategoriaId.Value);
        }

        return await query.Select(p => new ProductoDto
        {
            ProductoId = p.ProductoId,
            Nombre = p.Nombre,
            Descripcion = p.Descripcion,
            Precio = p.Precio,
            EstaDisponible = p.EstaDisponible,
            Tipo = p.Tipo,
            CategoriaId = p.CategoriaId,
            CategoriaNombre = p.Categoria.Nombre,
            CreadoEn = p.CreadoEn
        }).ToListAsync(cancellationToken);
    }
}