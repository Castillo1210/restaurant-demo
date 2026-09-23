using CaraNegra.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Categorias.Commands;

public record DeleteCategoriaCommand(int CategoriaId) : IRequest<bool>;

public class DeleteCategoriaCommandHandler : IRequestHandler<DeleteCategoriaCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public DeleteCategoriaCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<bool> Handle(DeleteCategoriaCommand request, CancellationToken cancellationToken)
    {
        var categoria = await _context.Categorias
        .FirstOrDefaultAsync(c => c.CategoriaId == request.CategoriaId, cancellationToken) ?? throw new KeyNotFoundException($"Categoría {request.CategoriaId} no encontrada.");

        categoria.EstaActivo = false;
        await _context.SaveChangesAsync(cancellationToken);
        
        return true;
    }
}