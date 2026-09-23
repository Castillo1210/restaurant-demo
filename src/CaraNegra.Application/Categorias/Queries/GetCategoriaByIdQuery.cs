using CaraNegra.Application.Categorias.DTOs;
using CaraNegra.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Categorias.Queries;

public record GetCategoriaByIdQuery(int CategoriaId) : IRequest<CategoriaDto>;

public class GetCategoriaByIdQueryHandler : IRequestHandler<GetCategoriaByIdQuery, CategoriaDto>
{
    private readonly IApplicationDbContext _context;

    public GetCategoriaByIdQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<CategoriaDto> Handle(GetCategoriaByIdQuery request, CancellationToken cancellationToken)
    {
        var categoria = await _context.Categorias
            .FirstOrDefaultAsync(c => c.CategoriaId == request.CategoriaId, cancellationToken) ?? throw new KeyNotFoundException($"Categoría {request.CategoriaId} no encontrada.");

        return new CategoriaDto
        {
            CategoriaId = categoria.CategoriaId,
            Nombre = categoria.Nombre,
            Descripcion = categoria.Descripcion,
            EstaActivo = categoria.EstaActivo,
            CreadoEn = categoria.CreadoEn
        };
    }
}