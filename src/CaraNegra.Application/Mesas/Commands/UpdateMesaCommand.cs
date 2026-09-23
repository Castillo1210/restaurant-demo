using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Mesas.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Mesas.Commands;

public record UpdateMesaCommand(int MesaId, UpdateMesaDto Dto) : IRequest<MesaDto>;

public class UpdateMesaCommandHandler : IRequestHandler<UpdateMesaCommand, MesaDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateMesaCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<MesaDto> Handle(UpdateMesaCommand request, CancellationToken cancellationToken)
    {
        var mesa = await _context.Mesas
            .FirstOrDefaultAsync(m => m.MesaId == request.MesaId, cancellationToken)
            ?? throw new KeyNotFoundException($"Mesa {request.MesaId} no encontrada");

        // Solo validar unicidad si el número realmente cambió, excluyendo la propia mesa
        // (antes esta comprobación vivía en el validador del DTO, que no tiene acceso al
        // MesaId de la ruta y por eso rechazaba guardar una mesa con su mismo número).
        if (request.Dto.NumeroMesa != mesa.NumeroMesa)
        {
            var yaExiste = await _context.Mesas
                .AnyAsync(m => m.NumeroMesa == request.Dto.NumeroMesa && m.MesaId != request.MesaId, cancellationToken);

            if (yaExiste)
                throw new InvalidOperationException($"Ya existe otra mesa con el número {request.Dto.NumeroMesa}");
        }

        mesa.NumeroMesa = request.Dto.NumeroMesa;
        mesa.Estado = request.Dto.Estado;

        await _context.SaveChangesAsync(cancellationToken);

        return new MesaDto
        {
            MesaId = mesa.MesaId,
            NumeroMesa = mesa.NumeroMesa,
            Estado = mesa.Estado,
            CreadoEn = mesa.CreadoEn
        };
    }
}