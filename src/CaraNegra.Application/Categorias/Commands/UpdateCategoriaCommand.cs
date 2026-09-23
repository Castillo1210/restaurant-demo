using CaraNegra.Application.Categorias.DTOs;
using CaraNegra.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Categorias.Commands;

public record UpdateCategoriaCommand(int CategoriaId, UpdateCategoriaDto Dto) : IRequest<CategoriaDto>;

public class UpdateCategoriaCommandHandler : IRequestHandler<UpdateCategoriaCommand, CategoriaDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateCategoriaCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<CategoriaDto> Handle(UpdateCategoriaCommand request, CancellationToken cancellationToken)
    {
        var categoria = await _context.Categorias
            .FirstOrDefaultAsync(c => c.CategoriaId == request.CategoriaId, cancellationToken) ?? throw new KeyNotFoundException($"Categoría {request.CategoriaId} no encontrada.");
        
        categoria.Nombre = request.Dto.Nombre;
        categoria.Descripcion = request.Dto.Descripcion;
        categoria.EstaActivo = request.Dto.EstaActivo;

        await _context.SaveChangesAsync(cancellationToken);

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