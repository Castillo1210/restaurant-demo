using CaraNegra.Application.Auth.Interfaces;
using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Usuarios.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Usuarios.Commands;

public record ChangePasswordCommand(int UsuarioId, ChangePasswordDto Dto) : IRequest<bool>;

public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordService _passwordService;

    public ChangePasswordCommandHandler(IApplicationDbContext context, IPasswordService passwordService)
    {
        _context = context;
        _passwordService = passwordService;
    }

    public async Task<bool> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.UsuarioId == request.UsuarioId, cancellationToken)
            ?? throw new KeyNotFoundException($"Usuario {request.UsuarioId} no encontrado");

        // Verificar contraseña actual
        if (!_passwordService.VerificarPassword(request.Dto.CurrentPassword, usuario.PasswordHash))
            throw new UnauthorizedAccessException("La contraseña actual es incorrecta");

        // Actualizar contraseña
        usuario.PasswordHash = _passwordService.HashPassword(request.Dto.NewPassword);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}