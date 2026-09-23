using CaraNegra.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.MetodosPago.Commands;

public record DeleteMetodoPagoCommand(int MetodoPagoId) : IRequest<bool>;

public class DeleteMetodoPagoCommandHandler : IRequestHandler<DeleteMetodoPagoCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public DeleteMetodoPagoCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<bool> Handle(DeleteMetodoPagoCommand request, CancellationToken cancellationToken)
    {
        var metodoPago = await _context.MetodosPago
            .FirstOrDefaultAsync(m => m.MetodoPagoId == request.MetodoPagoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Método de pago {request.MetodoPagoId} no encontrado.");

        // Soft-delete: nunca se borra físicamente, porque los pagos históricos
        // siguen referenciando este método (FK con DeleteBehavior.Restrict).
        metodoPago.EstaActivo = false;
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
