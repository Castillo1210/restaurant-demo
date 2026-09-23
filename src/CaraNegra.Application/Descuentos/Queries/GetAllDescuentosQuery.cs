using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Descuentos.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Descuentos.Queries;

/// <summary>
/// Lista de descuentos. Con SoloVigentes=true (lo que usa Caja al cobrar) solo trae los
/// activos y dentro de su rango de fechas de vigencia (si tiene uno definido) — comparado
/// contra la fecha calendario de hoy en Lima (UTC-5), igual que el resto del sistema.
/// </summary>
public record GetAllDescuentosQuery(bool SoloVigentes = false) : IRequest<List<DescuentoDto>>;

public class GetAllDescuentosQueryHandler : IRequestHandler<GetAllDescuentosQuery, List<DescuentoDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAllDescuentosQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<DescuentoDto>> Handle(GetAllDescuentosQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Descuentos.AsQueryable();

        if (request.SoloVigentes)
        {
            var hoyEnLima = DateTime.UtcNow.AddHours(-5).Date;
            query = query.Where(d => d.EstaActivo
                && (d.FechaInicio == null || d.FechaInicio.Value.Date <= hoyEnLima)
                && (d.FechaFin == null || d.FechaFin.Value.Date >= hoyEnLima));
        }

        return await query
            .OrderBy(d => d.Nombre)
            .Select(d => new DescuentoDto
            {
                DescuentoId = d.Descuentoid,
                Nombre = d.Nombre,
                EsPorcentaje = d.EsPorcentaje,
                Valor = d.Valor,
                EstaActivo = d.EstaActivo,
                FechaInicio = d.FechaInicio,
                FechaFin = d.FechaFin,
                CreadoEn = d.CreadoEn
            })
            .ToListAsync(cancellationToken);
    }
}
