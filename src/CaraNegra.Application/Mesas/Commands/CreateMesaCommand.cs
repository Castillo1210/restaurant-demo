using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Mesas.DTOs;
using CaraNegra.Domain.Entities;
using CaraNegra.Domain.Enums;
using MediatR;

namespace CaraNegra.Application.Mesas.Commands;

public record CreateMesaCommand(CreateMesaDto Dto) : IRequest<MesaDto>;

public class CreateMesaCommandHandler : IRequestHandler<CreateMesaCommand, MesaDto>
{
    private readonly IApplicationDbContext _context;

    public CreateMesaCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<MesaDto> Handle(CreateMesaCommand request, CancellationToken cancellationToken)
    {
        // Crear nueva mesa con estado inicial Disponible
        var mesa = new Mesa
        {
            NumeroMesa = request.Dto.NumeroMesa,
            Estado = EstadoMesa.Disponible
        };

        _context.Mesas.Add(mesa);
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