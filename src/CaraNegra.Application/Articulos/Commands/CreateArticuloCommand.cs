using CaraNegra.Application.Articulos.DTOs;
using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Domain.Entities;
using MediatR;

namespace CaraNegra.Application.Articulos.Commands;

public record CreateArticuloCommand(CreateArticuloDto Dto) : IRequest<ArticuloDto>;

public class CreateArticuloCommandHandler : IRequestHandler<CreateArticuloCommand, ArticuloDto>
{
    private readonly IApplicationDbContext _context;

    public CreateArticuloCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<ArticuloDto> Handle(CreateArticuloCommand request, CancellationToken cancellationToken)
    {
        var articulo = new Articulo
        {
            Nombre = request.Dto.Nombre,
            Descripcion = request.Dto.Descripcion,
            Precio = request.Dto.Precio,
            Tipo = request.Dto.Tipo,
            CategoriaId = request.Dto.CategoriaId,
            Stock = request.Dto.StockInicial,
            Activo = true
        };

        _context.Articulos.Add(articulo);
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
