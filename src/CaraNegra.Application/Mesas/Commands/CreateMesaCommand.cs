using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Mesas.DTOs;
using CaraNegra.Domain.Entities;
using CaraNegra.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Mesas.Commands;

public record CreateMesaCommand(CreateMesaDto Dto) : IRequest<MesaDto>;

public class CreateMesaCommandHandler : IRequestHandler<CreateMesaCommand, MesaDto>
{
    private readonly IApplicationDbContext _context;

    public CreateMesaCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<MesaDto> Handle(CreateMesaCommand request, CancellationToken cancellationToken)
    {
        var numero = request.Dto.NumeroMesa.Trim();

        var salon = await _context.Salones
            .FirstOrDefaultAsync(s => s.SalonId == request.Dto.SalonId, cancellationToken)
            ?? throw new KeyNotFoundException($"Salón {request.Dto.SalonId} no encontrado");

        // La numeración es por salón: "Mesa 1" puede existir en Terraza y en Jardín a la vez.
        var yaExiste = await _context.Mesas
            .AnyAsync(m => m.SalonId == salon.SalonId && m.NumeroMesa == numero, cancellationToken);
        if (yaExiste)
            throw new InvalidOperationException($"Ya existe la mesa {numero} en el salón {salon.Nombre}");

        // Crear nueva mesa con estado inicial Disponible
        var mesa = new Mesa
        {
            NumeroMesa = numero,
            SalonId = salon.SalonId,
            Salon = salon,
            Estado = EstadoMesa.Disponible
        };

        _context.Mesas.Add(mesa);
        await _context.SaveChangesAsync(cancellationToken);

        return MesaMapper.ToDto(mesa);
    }
}
