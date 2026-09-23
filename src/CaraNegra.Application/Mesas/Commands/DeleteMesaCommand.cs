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
        var mesa = await _context.Mesas
            .FirstOrDefaultAsync(m => m.MesaId == request.MesaId, cancellationToken)
            ?? throw new KeyNotFoundException($"Mesa {request.MesaId} no encontrada");

        // Soft delete: cambiar estado a Mantenimiento
        mesa.Estado = EstadoMesa.Mantenimiento;
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}