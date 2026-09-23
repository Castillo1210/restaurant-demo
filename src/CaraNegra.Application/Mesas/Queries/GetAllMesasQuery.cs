using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Mesas.DTOs;
using CaraNegra.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Mesas.Queries;

public record GetAllMesasQuery(bool SoloDisponibles = false) : IRequest<List<MesaDto>>;

public class GetAllMesasQueryHandler : IRequestHandler<GetAllMesasQuery, List<MesaDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAllMesasQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<MesaDto>> Handle(GetAllMesasQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Mesas.AsQueryable();

        if (request.SoloDisponibles)
        {
            query = query.Where(m => m.Estado == EstadoMesa.Disponible);
        }

        return await query
            .OrderBy(m => m.NumeroMesa)
            .Select(m => new MesaDto
            {
                MesaId = m.MesaId,
                NumeroMesa = m.NumeroMesa,
                Estado = m.Estado,
                CreadoEn = m.CreadoEn
            })
            .ToListAsync(cancellationToken);
    }
}