using CaraNegra.Application.Auth.Interfaces;
using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Usuarios.DTOs;
using CaraNegra.Domain.Entities;
using CaraNegra.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Usuarios.Commands;

public record CreateUsuarioCommand(CreateUsuarioDto Dto) : IRequest<UsuarioDto>;

public class CreateUsuarioCommandHandler : IRequestHandler<CreateUsuarioCommand, UsuarioDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordService _passwordService;

    public CreateUsuarioCommandHandler(IApplicationDbContext context, IPasswordService passwordService)
    {
        _context = context;
        _passwordService = passwordService;
    }

    public async Task<UsuarioDto> Handle(CreateUsuarioCommand request, CancellationToken cancellationToken)
    {
        // Verificar que el nombre de usuario no exista
        if (await _context.Usuarios.AnyAsync(u => u.NombreUsuario == request.Dto.NombreUsuario, cancellationToken))
        {
            throw new InvalidOperationException("El nombre de usuario ya existe");
        }

        var rol = await _context.Roles
            .FirstOrDefaultAsync(r => r.RolId == request.Dto.RolId, cancellationToken)
            ?? throw new KeyNotFoundException($"Rol {request.Dto.RolId} no encontrado");

        var usuario = new Usuario
        {
            NombreUsuario = request.Dto.NombreUsuario,
            NombreCompleto = request.Dto.NombreCompleto,
            PasswordHash = _passwordService.HashPassword(request.Dto.Password),
            RolId = request.Dto.RolId,
            EsVerificado = true
        };

        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync(cancellationToken);

        var usuarioCreado = await _context.Usuarios
            .Include(u => u.Rol)
            .FirstAsync(u => u.UsuarioId == usuario.UsuarioId, cancellationToken);

        return MapToDto(usuarioCreado);
    }

    private UsuarioDto MapToDto(Usuario usuario)
    {
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