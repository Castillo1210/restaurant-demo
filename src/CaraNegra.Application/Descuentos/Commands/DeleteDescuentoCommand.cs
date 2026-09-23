using CaraNegra.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Descuentos.Commands;

public record DeleteDescuentoCommand(int DescuentoId) : IRequest<bool>;

public class DeleteDescuentoCommandHandler : IRequestHandler<DeleteDescuentoCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public DeleteDescuentoCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<bool> Handle(DeleteDescuentoCommand request, CancellationToken cancellationToken)
    {
        var descuento = await _context.Descuentos
            .FirstOrDefaultAsync(d => d.Descuentoid == request.DescuentoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Descuento {request.DescuentoId} no encontrado");

        // Soft-delete: nunca se borra físicamente, porque pedidos históricos (DetalleDescuento)
        // pueden seguir referenciando este descuento (FK con DeleteBehavior.Restrict).
        descuento.EstaActivo = false;
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
