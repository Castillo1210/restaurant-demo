using CaraNegra.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Cremas.Commands;

public record DeleteCremaCommand(int CremaId) : IRequest<bool>;

public class DeleteCremaCommandHandler : IRequestHandler<DeleteCremaCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public DeleteCremaCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<bool> Handle(DeleteCremaCommand request, CancellationToken cancellationToken)
    {
        var crema = await _context.Cremas
            .FirstOrDefaultAsync(c => c.CremaId == request.CremaId, cancellationToken)
            ?? throw new KeyNotFoundException($"Crema {request.CremaId} no encontrada.");

        // Soft-delete: no se borra físicamente, ya que pedidos históricos pueden mencionarla
        // en su texto de notas (aunque no exista una FK formal hacia ella).
        crema.EstaActivo = false;
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
