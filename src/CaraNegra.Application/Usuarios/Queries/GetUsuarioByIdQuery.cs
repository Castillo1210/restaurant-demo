using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Usuarios.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Usuarios.Queries;

public record GetUsuarioByIdQuery(int UsuarioId) : IRequest<UsuarioDto>;

public class GetUsuarioByIdQueryHandler : IRequestHandler<GetUsuarioByIdQuery, UsuarioDto>
{
    private readonly IApplicationDbContext _context;

    public GetUsuarioByIdQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<UsuarioDto> Handle(GetUsuarioByIdQuery request, CancellationToken cancellationToken)
    {
        var usuario = await _context.Usuarios
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.UsuarioId == request.UsuarioId, cancellationToken)
            ?? throw new KeyNotFoundException($"Usuario {request.UsuarioId} no encontrado");

        return new UsuarioDto
        {
            UsuarioId = usuario.UsuarioId,
            NombreUsuario = usuario.NombreUsuario,
            NombreCompleto = usuario.NombreCompleto,
            RolId = usuario.RolId,
            RolNombre = usuario.Rol?.Nombre ?? string.Empty,
            EsVerificado = usuario.EsVerificado,
            UltimoAccesoEn = usuario.UltimoAccesoEn,
            CreadoEn = usuario.CreadoEn
        };
    }
}