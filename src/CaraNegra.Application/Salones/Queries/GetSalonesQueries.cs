using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Salones.DTOs;
using CaraNegra.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Salones.Queries;

public record GetAllSalonesQuery(bool SoloActivos = false) : IRequest<List<SalonDto>>;

public class GetAllSalonesQueryHandler : IRequestHandler<GetAllSalonesQuery, List<SalonDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAllSalonesQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<SalonDto>> Handle(GetAllSalonesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Salones.AsQueryable();
        if (request.SoloActivos)
            query = query.Where(s => s.EstaActivo);

        return await query
            .OrderBy(s => s.Orden).ThenBy(s => s.Nombre)
            .Select(s => new SalonDto
            {
                SalonId = s.SalonId,
                Nombre = s.Nombre,
                Descripcion = s.Descripcion,
                Orden = s.Orden,
                EstaActivo = s.EstaActivo,
                CantidadMesas = s.Mesas.Count(m => m.Estado != EstadoMesa.Mantenimiento),
                CreadoEn = s.CreadoEn
            })
            .ToListAsync(cancellationToken);
    }
}

public record GetSalonByIdQuery(int SalonId) : IRequest<SalonDto>;

public class GetSalonByIdQueryHandler : IRequestHandler<GetSalonByIdQuery, SalonDto>
{
    private readonly IApplicationDbContext _context;

    public GetSalonByIdQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<SalonDto> Handle(GetSalonByIdQuery request, CancellationToken cancellationToken)
    {
        return await _context.Salones
            .Where(s => s.SalonId == request.SalonId)
            .Select(s => new SalonDto
            {
                SalonId = s.SalonId,
                Nombre = s.Nombre,
                Descripcion = s.Descripcion,
                Orden = s.Orden,
                EstaActivo = s.EstaActivo,
                CantidadMesas = s.Mesas.Count(m => m.Estado != EstadoMesa.Mantenimiento),
                CreadoEn = s.CreadoEn
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Salón {request.SalonId} no encontrado");
    }
}
