using CaraNegra.Application.Articulos.DTOs;
using CaraNegra.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Articulos.Commands;

public record UpdateArticuloCommand(int ArticuloId, UpdateArticuloDto Dto) : IRequest<ArticuloDto>;

public class UpdateArticuloCommandHandler : IRequestHandler<UpdateArticuloCommand, ArticuloDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateArticuloCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<ArticuloDto> Handle(UpdateArticuloCommand request, CancellationToken cancellationToken)
    {
        var articulo = await _context.Articulos
            .FirstOrDefaultAsync(a => a.ArticuloId == request.ArticuloId, cancellationToken)
            ?? throw new KeyNotFoundException($"Artículo {request.ArticuloId} no encontrado.");

        articulo.Nombre = request.Dto.Nombre;
        articulo.Descripcion = request.Dto.Descripcion;
        articulo.Precio = request.Dto.Precio;
        articulo.Tipo = request.Dto.Tipo;
        articulo.CategoriaId = request.Dto.CategoriaId;
        articulo.Activo = request.Dto.Activo;
        // El Stock NO se toca aquí — solo cambia vía RegistrarMovimientoArticuloCommand.

        await _context.SaveChangesAsync(cancellationToken);

        var categoria = await _context.Categorias.FindAsync(new object[] { articulo.CategoriaId }, cancellationToken);

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
            CategoriaNombre = categoria?.Nombre ?? string.Empty,
            CreadoEn = articulo.CreadoEn
        };
    }
}
