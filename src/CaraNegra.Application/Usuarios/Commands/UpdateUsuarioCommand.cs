using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Usuarios.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Usuarios.Commands;

public record UpdateUsuarioCommand(int UsuarioId, UpdateUsuarioDto Dto) : IRequest<UsuarioDto>;

public class UpdateUsuarioCommandHandler : IRequestHandler<UpdateUsuarioCommand, UsuarioDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateUsuarioCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<UsuarioDto> Handle(UpdateUsuarioCommand request, CancellationToken cancellationToken)
    {
        var usuario = await _context.Usuarios
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.UsuarioId == request.UsuarioId, cancellationToken)
            ?? throw new KeyNotFoundException($"Usuario {request.UsuarioId} no encontrado");

        var rol = await _context.Roles
            .FirstOrDefaultAsync(r => r.RolId == request.Dto.RolId, cancellationToken)
            ?? throw new KeyNotFoundException($"Rol {request.Dto.RolId} no encontrado");

        usuario.NombreCompleto = request.Dto.NombreCompleto;
        usuario.RolId = rol.RolId;
        usuario.EsVerificado = request.Dto.EsVerificado;

        await _context.SaveChangesAsync(cancellationToken);

        return new UsuarioDto
        {
            UsuarioId = usuario.UsuarioId,
            NombreUsuario = usuario.NombreUsuario,
            NombreCompleto = usuario.NombreCompleto,
            RolId = usuario.RolId,
            RolNombre = rol.Nombre,
            EsVerificado = usuario.EsVerificado,
            UltimoAccesoEn = usuario.UltimoAccesoEn,
            CreadoEn = usuario.CreadoEn
        };
    }
}