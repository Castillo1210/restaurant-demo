using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Roles.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Roles.Queries;

/// <summary>
/// Lista los roles existentes (ADMIN, MOZO, CAJERO — sembrados por AdminBootstrapSeeder).
/// Son de solo lectura desde la API: los controladores de la aplicación autorizan por
/// nombre de rol hardcodeado (p.ej. [Authorize(Roles = "ADMIN")]), así que crear un rol
/// nuevo aquí no le otorgaría ningún permiso real. Esta consulta solo existe para que el
/// panel de administración pueda mostrar los roles disponibles al crear/editar un usuario.
/// </summary>
public record GetAllRolesQuery : IRequest<List<RolDto>>;

public class GetAllRolesQueryHandler : IRequestHandler<GetAllRolesQuery, List<RolDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAllRolesQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<RolDto>> Handle(GetAllRolesQuery request, CancellationToken cancellationToken)
    {
        return await _context.Roles
            .OrderBy(r => r.Nombre)
            .Select(r => new RolDto
            {
                RolId = r.RolId,
                Nombre = r.Nombre,
                Descripcion = r.Descripcion
            })
            .ToListAsync(cancellationToken);
    }
}
