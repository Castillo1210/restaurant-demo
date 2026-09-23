using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Descuentos.DTOs;
using CaraNegra.Domain.Entities;
using MediatR;

namespace CaraNegra.Application.Descuentos.Commands;

public record CreateDescuentoCommand(CreateDescuentoDto Dto) : IRequest<DescuentoDto>;

public class CreateDescuentoCommandHandler : IRequestHandler<CreateDescuentoCommand, DescuentoDto>
{
    private readonly IApplicationDbContext _context;

    public CreateDescuentoCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<DescuentoDto> Handle(CreateDescuentoCommand request, CancellationToken cancellationToken)
    {
        var descuento = new Descuento
        {
            Nombre = request.Dto.Nombre,
            EsPorcentaje = request.Dto.EsPorcentaje,
            Valor = request.Dto.Valor,
            FechaInicio = request.Dto.FechaInicio,
            FechaFin = request.Dto.FechaFin,
            EstaActivo = true
        };

        _context.Descuentos.Add(descuento);
        await _context.SaveChangesAsync(cancellationToken);

        return MapToDto(descuento);
    }

    private static DescuentoDto MapToDto(Descuento d) => new()
    {
        DescuentoId = d.Descuentoid,
        Nombre = d.Nombre,
        EsPorcentaje = d.EsPorcentaje,
        Valor = d.Valor,
        EstaActivo = d.EstaActivo,
        FechaInicio = d.FechaInicio,
        FechaFin = d.FechaFin,
        CreadoEn = d.CreadoEn
    };
}
