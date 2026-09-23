using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Productos.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Productos.Queries;

public record GetProductoByIdQuery(int ProductoId) : IRequest<ProductoDto>;

public class GetProductoByIdQueryHandler : IRequestHandler<GetProductoByIdQuery, ProductoDto>
{
    private readonly IApplicationDbContext _context;

    public GetProductoByIdQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<ProductoDto> Handle(GetProductoByIdQuery request, CancellationToken cancellationToken)
    {
        var producto = await _context.Productos
            .Include(p => p.Categoria)
            .FirstOrDefaultAsync(p => p.ProductoId == request.ProductoId, cancellationToken) 
            ?? throw new KeyNotFoundException($"Producto {request.ProductoId} no encontrado.");

        return new ProductoDto
        {
            ProductoId = producto.ProductoId,
            Nombre = producto.Nombre,
            Descripcion = producto.Descripcion,
            Precio = producto.Precio,
            EstaDisponible = producto.EstaDisponible,
            Tipo = producto.Tipo,
            CategoriaId = producto.CategoriaId,
            CategoriaNombre = producto.Categoria?.Nombre ?? string.Empty,
            CreadoEn = producto.CreadoEn
        };
    }
}