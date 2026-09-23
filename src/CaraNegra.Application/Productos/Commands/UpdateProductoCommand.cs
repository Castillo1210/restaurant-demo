using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Productos.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Productos.Commands;

public record UpdateProductoCommand(int ProductoId, UpdateProductoDto Dto) : IRequest<ProductoDto>;

public class UpdateProductoCommandHandler : IRequestHandler<UpdateProductoCommand, ProductoDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateProductoCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<ProductoDto> Handle(UpdateProductoCommand request, CancellationToken cancellationToken)
    {
        var producto = await _context.Productos
            .FirstOrDefaultAsync(p => p.ProductoId == request.ProductoId, cancellationToken) 
            ?? throw new KeyNotFoundException($"Producto {request.ProductoId} no encontrado.");
        
        producto.Nombre = request.Dto.Nombre;
        producto.Descripcion = request.Dto.Descripcion;
        producto.Precio = request.Dto.Precio;
        producto.EstaDisponible = request.Dto.EstaDisponible;
        producto.Tipo = request.Dto.Tipo;
        producto.CategoriaId = request.Dto.CategoriaId;

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