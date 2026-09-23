using CaraNegra.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Articulos.Commands;

public record DeleteArticuloCommand(int ArticuloId) : IRequest<bool>;

public class DeleteArticuloCommandHandler : IRequestHandler<DeleteArticuloCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public DeleteArticuloCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<bool> Handle(DeleteArticuloCommand request, CancellationToken cancellationToken)
    {
        var articulo = await _context.Articulos
            .FirstOrDefaultAsync(a => a.ArticuloId == request.ArticuloId, cancellationToken)
            ?? throw new KeyNotFoundException($"Artículo {request.ArticuloId} no encontrado.");

        articulo.Activo = false;
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
