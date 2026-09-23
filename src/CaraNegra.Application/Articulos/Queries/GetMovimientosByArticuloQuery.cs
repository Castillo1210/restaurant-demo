using CaraNegra.Application.Articulos.DTOs;
using CaraNegra.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Articulos.Queries;

public record GetMovimientosByArticuloQuery(int ArticuloId) : IRequest<List<MovimientoArticuloDto>>;

public class GetMovimientosByArticuloQueryHandler : IRequestHandler<GetMovimientosByArticuloQuery, List<MovimientoArticuloDto>>
{
    private readonly IApplicationDbContext _context;

    public GetMovimientosByArticuloQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<MovimientoArticuloDto>> Handle(GetMovimientosByArticuloQuery request, CancellationToken cancellationToken)
    {
        var movimientos = await _context.MovimientosArticulo
            .Include(m => m.Articulo)
            .Include(m => m.Usuario)
            .Where(m => m.ArticuloId == request.ArticuloId)
            .OrderByDescending(m => m.CreadoEn)
            .ToListAsync(cancellationToken);

        return movimientos.Select(m => new MovimientoArticuloDto
        {
            MovimientoArticuloId = m.MovimientoArticuloId,
            ArticuloId = m.ArticuloId,
            ArticuloNombre = m.Articulo?.Nombre ?? string.Empty,
            TipoMovimiento = m.TipoMovimiento.ToString(),
            Cantidad = m.Cantidad,
            Balance = m.Balance,
            ReferenciaCod = m.ReferenciaCod,
            Notas = m.Notas,
            UsuarioId = m.UsuarioId,
            UsuarioNombre = m.Usuario?.NombreCompleto ?? string.Empty,
            CreadoEn = m.CreadoEn
        }).ToList();
    }
}
