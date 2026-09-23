using CaraNegra.Application.Auth.DTOs;
using CaraNegra.Application.Auth.Interfaces;
using Microsoft.EntityFrameworkCore;
using MediatR;
using CaraNegra.Application.Common.Interfaces;

namespace CaraNegra.Application.Auth.Commands;

public record LoginCommand(LoginRequest Request) : IRequest<LoginResponse>;

public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly IPasswordService _passwordService;

    public LoginCommandHandler(IApplicationDbContext context, IJwtService jwtService, IPasswordService passwordService)
    {
        _context = context;
        _jwtService = jwtService;
        _passwordService = passwordService;
    }

    public async Task<LoginResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var usuario = await _context.Usuarios.Include(u => u.Rol).FirstOrDefaultAsync(u => u.NombreUsuario == request.Request.NombreUsuario, cancellationToken);

        if (usuario is null || !_passwordService.VerificarPassword(request.Request.Password, usuario.PasswordHash))
        {
            throw new UnauthorizedAccessException("Usuario o contraseña incorrectos.");
        }

        // Un usuario desactivado (EsVerificado = false, ver DeleteUsuarioCommand) no puede
        // iniciar sesión. Antes de esta verificación, desactivar a un empleado desde el panel
        // de administración no le impedía seguir usando el sistema.
        if (!usuario.EsVerificado)
        {
            throw new UnauthorizedAccessException("Este usuario está desactivado. Contacta a un administrador.");
        }

        // Actualizar último acceso
        usuario.UltimoAccesoEn = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return new LoginResponse
        {
            Token = _jwtService.GenerarToken(usuario),
            NombreUsuario = usuario.NombreUsuario,
            NombreCompleto = usuario.NombreCompleto,
            Rol = usuario.Rol.Nombre,
            Expiracion = _jwtService.ObtenerExpiracion()
        };
    }
}