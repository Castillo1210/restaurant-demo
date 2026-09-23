using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Usuarios.DTOs;
using CaraNegra.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Usuarios.Queries;

public record GetUsuariosByRolQuery(int RolId, int Page = 1, int PageSize = 20) : IRequest<PagedResult<UsuarioDto>>;

public class GetUsuariosByRolQueryHandler : IRequestHandler<GetUsuariosByRolQuery, PagedResult<UsuarioDto>>
{
    private readonly IApplicationDbContext _context;

    public GetUsuariosByRolQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<PagedResult<UsuarioDto>> Handle(GetUsuariosByRolQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Usuarios
            .Include(u => u.Rol)
            .Where(u => u.RolId == request.RolId);

        var total = await query.CountAsync(cancellationToken);

        var usuarios = await query
            .OrderBy(u => u.NombreUsuario)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var items = usuarios.Select(MapToDto).ToList();

        return new PagedResult<UsuarioDto>
        {
            Items = items,
            TotalCount = total,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    private UsuarioDto MapToDto(CaraNegra.Domain.Entities.Usuario u)
    {
        return new UsuarioDto
        {
            UsuarioId = u.UsuarioId,
            NombreUsuario = u.NombreUsuario,
            NombreCompleto = u.NombreCompleto,
            RolId = u.RolId,
            RolNombre = u.Rol?.Nombre ?? string.Empty,
            EsVerificado = u.EsVerificado,
            UltimoAccesoEn = u.UltimoAccesoEn,
            CreadoEn = u.CreadoEn
        };
    }
}