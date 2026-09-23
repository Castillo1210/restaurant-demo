using CaraNegra.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Productos.Commands;

public record DeleteProductoCommand(int ProductoId) : IRequest<bool>;

public class DeleteProductoCommandHandler : IRequestHandler<DeleteProductoCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public DeleteProductoCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<bool> Handle(DeleteProductoCommand request, CancellationToken cancellationToken)
    {
        var producto = await _context.Productos
            .FirstOrDefaultAsync(p => p.ProductoId == request.ProductoId, cancellationToken) 
            ?? throw new KeyNotFoundException($"Producto {request.ProductoId} no encontrado.");

        producto.EstaDisponible = false;
        await _context.SaveChangesAsync(cancellationToken);
        
        return true;
    }
}