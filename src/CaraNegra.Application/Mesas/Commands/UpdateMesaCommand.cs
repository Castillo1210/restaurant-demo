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
        // Bloquea la mesa: si a la vez un mozo abre un pedido en ella, uno espera al otro.
        await using var tx = await _context.IniciarTransaccionAsync(cancellationToken);
        await _context.BloquearMesaAsync(request.MesaId, cancellationToken);

        var mesa = await _context.Mesas
            .Include(m => m.Salon)
            .FirstOrDefaultAsync(m => m.MesaId == request.MesaId, cancellationToken)
            ?? throw new KeyNotFoundException($"Mesa {request.MesaId} no encontrada");

        var salon = await _context.Salones
            .FirstOrDefaultAsync(s => s.SalonId == request.Dto.SalonId, cancellationToken)
            ?? throw new KeyNotFoundException($"Salón {request.Dto.SalonId} no encontrado");

        var numero = request.Dto.NumeroMesa.Trim();

        // Solo validar unicidad si cambió el número o el salón, excluyendo la propia mesa
        // (la validación vive aquí y no en el validador porque este necesita el MesaId de la ruta).
        if (numero != mesa.NumeroMesa || salon.SalonId != mesa.SalonId)
        {
            var yaExiste = await _context.Mesas
                .AnyAsync(m => m.SalonId == salon.SalonId && m.NumeroMesa == numero && m.MesaId != mesa.MesaId, cancellationToken);

            if (yaExiste)
                throw new InvalidOperationException($"Ya existe otra mesa {numero} en el salón {salon.Nombre}");
        }

        mesa.NumeroMesa = numero;
        mesa.Estado = await MesaOcupacion.EstadoAlEditarAsync(_context, mesa, request.Dto.Estado, cancellationToken);
        mesa.SalonId = salon.SalonId;
        mesa.Salon = salon;

        await _context.SaveChangesAsync(cancellationToken);
        await tx.ConfirmarAsync(cancellationToken);

        return MesaMapper.ToDto(mesa);
    }
}
