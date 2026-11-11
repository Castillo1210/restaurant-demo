using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Mesas.Commands;

public record DeleteMesaCommand(int MesaId) : IRequest<bool>;

public class DeleteMesaCommandHandler : IRequestHandler<DeleteMesaCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public DeleteMesaCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<bool> Handle(DeleteMesaCommand request, CancellationToken cancellationToken)
    {
        await using var tx = await _context.IniciarTransaccionAsync(cancellationToken);
        await _context.BloquearMesaAsync(request.MesaId, cancellationToken);

        var mesa = await _context.Mesas
            .FirstOrDefaultAsync(m => m.MesaId == request.MesaId, cancellationToken)
            ?? throw new KeyNotFoundException($"Mesa {request.MesaId} no encontrada");

        // Soft delete: Mantenimiento (no se puede con un pedido abierto en la mesa).
        mesa.Estado = await MesaOcupacion.EstadoAlEditarAsync(_context, mesa, EstadoMesa.Mantenimiento, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await tx.ConfirmarAsync(cancellationToken);

        return true;
    }
}