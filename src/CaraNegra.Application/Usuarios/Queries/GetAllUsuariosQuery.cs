using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Usuarios.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Usuarios.Queries;

public record GetAllUsuariosQuery(int Page = 1, int PageSize = 20, string? Search = null) : IRequest<PagedResult<UsuarioDto>>;

public class GetAllUsuariosQueryHandler : IRequestHandler<GetAllUsuariosQuery, PagedResult<UsuarioDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAllUsuariosQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<PagedResult<UsuarioDto>> Handle(GetAllUsuariosQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Usuarios
            .Include(u => u.Rol)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.ToLower();
            query = query.Where(u => u.NombreUsuario.ToLower().Contains(search) 
                || u.NombreCompleto.ToLower().Contains(search));
        }

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

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}