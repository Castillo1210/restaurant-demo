using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Mesas.DTOs;
using CaraNegra.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Mesas.Queries;

/// <param name="SoloDisponibles">Solo mesas en estado Disponible.</param>
/// <param name="SalonId">Filtra por salón (null = todos).</param>
public record GetAllMesasQuery(bool SoloDisponibles = false, int? SalonId = null) : IRequest<List<MesaDto>>;

public class GetAllMesasQueryHandler : IRequestHandler<GetAllMesasQuery, List<MesaDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAllMesasQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<MesaDto>> Handle(GetAllMesasQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Mesas.Include(m => m.Salon).AsQueryable();

        if (request.SoloDisponibles)
        {
            query = query.Where(m => m.Estado == EstadoMesa.Disponible);
        }

        if (request.SalonId.HasValue)
        {
            query = query.Where(m => m.SalonId == request.SalonId.Value);
        }

        var mesas = await query.ToListAsync(cancellationToken);

        // Quién atiende cada mesa ocupada: el mozo del pedido activo (no Entregado ni Cancelado).
        // Si una mesa tuviera más de un pedido activo, se toma el más reciente.
        var pedidosActivos = await _context.Pedidos
            .Where(MesaOcupacion.PedidoAbierto)
            .Select(p => new { p.MesaId, p.PedidoId, p.UsuarioId, Mozo = p.Usuario.NombreCompleto, p.CreadoEn })
            .ToListAsync(cancellationToken);

        var atencionPorMesa = pedidosActivos
            .GroupBy(p => p.MesaId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.CreadoEn).First());

        // Orden por salón y luego orden natural del número ("2" antes que "10"), en memoria.
        return MesaMapper.OrdenNatural(mesas)
            .Select(m =>
            {
                var dto = MesaMapper.ToDto(m);
                if (atencionPorMesa.TryGetValue(m.MesaId, out var atencion))
                {
                    dto.PedidoActivoId = atencion.PedidoId;
                    dto.AtendidoPorUsuarioId = atencion.UsuarioId;
                    dto.AtendidoPor = atencion.Mozo;
                }
                return dto;
            })
            .ToList();
    }
}
