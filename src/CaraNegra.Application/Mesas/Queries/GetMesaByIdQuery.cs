using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Mesas.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Mesas.Queries;

public record GetMesaByIdQuery(int MesaId) : IRequest<MesaDto>;

public class GetMesaByIdQueryHandler : IRequestHandler<GetMesaByIdQuery, MesaDto>
{
    private readonly IApplicationDbContext _context;

    public GetMesaByIdQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<MesaDto> Handle(GetMesaByIdQuery request, CancellationToken cancellationToken)
    {
        var mesa = await _context.Mesas
            .Include(m => m.Salon)
            .FirstOrDefaultAsync(m => m.MesaId == request.MesaId, cancellationToken)
            ?? throw new KeyNotFoundException($"Mesa {request.MesaId} no encontrada");

        return MesaMapper.ToDto(mesa);
    }
}
