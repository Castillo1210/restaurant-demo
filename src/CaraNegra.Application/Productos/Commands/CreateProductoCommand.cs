using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Productos.DTOs;
using CaraNegra.Domain.Entities;
using MediatR;

namespace CaraNegra.Application.Productos.Commands;

public record CreateProductoCommand(CreateProductoDto Dto) : IRequest<ProductoDto>;

public class CreateProductoCommandHandler : IRequestHandler<CreateProductoCommand, ProductoDto>
{
    private readonly IApplicationDbContext _context;

    public CreateProductoCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<ProductoDto> Handle(CreateProductoCommand request, CancellationToken cancellationToken)
    {
        var producto = new Producto
        {
            Nombre = request.Dto.Nombre,
            Descripcion = request.Dto.Descripcion,
            Precio = request.Dto.Precio,
            Tipo = request.Dto.Tipo,
            CategoriaId = request.Dto.CategoriaId,
            EstaDisponible = true
        };

        _context.Productos.Add(producto);
        await _context.SaveChangesAsync(cancellationToken);

        var categoria = await _context.Categorias.FindAsync(new object[] { producto.CategoriaId }, cancellationToken);

        return new ProductoDto
        {
            ProductoId = producto.ProductoId,
            Nombre = producto.Nombre,
            Descripcion = producto.Descripcion,
            Precio = producto.Precio,
            EstaDisponible = producto.EstaDisponible,
            Tipo = producto.Tipo,
            CategoriaId = producto.CategoriaId,
            CategoriaNombre = categoria?.Nombre ?? string.Empty,
            CreadoEn = producto.CreadoEn
        };
    }
}