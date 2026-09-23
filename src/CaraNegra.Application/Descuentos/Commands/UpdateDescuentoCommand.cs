using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Descuentos.DTOs;
using CaraNegra.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Descuentos.Commands;

public record UpdateDescuentoCommand(int DescuentoId, UpdateDescuentoDto Dto) : IRequest<DescuentoDto>;

public class UpdateDescuentoCommandHandler : IRequestHandler<UpdateDescuentoCommand, DescuentoDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateDescuentoCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<DescuentoDto> Handle(UpdateDescuentoCommand request, CancellationToken cancellationToken)
    {
        var descuento = await _context.Descuentos
            .FirstOrDefaultAsync(d => d.Descuentoid == request.DescuentoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Descuento {request.DescuentoId} no encontrado");

        descuento.Nombre = request.Dto.Nombre;
        descuento.EsPorcentaje = request.Dto.EsPorcentaje;
        descuento.Valor = request.Dto.Valor;
        descuento.EstaActivo = request.Dto.EstaActivo;
        descuento.FechaInicio = request.Dto.FechaInicio;
        descuento.FechaFin = request.Dto.FechaFin;

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
