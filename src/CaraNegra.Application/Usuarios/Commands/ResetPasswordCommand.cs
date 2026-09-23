using CaraNegra.Application.Auth.Interfaces;
using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Usuarios.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Usuarios.Commands;

public record ResetPasswordCommand(int UsuarioId, ResetPasswordDto Dto) : IRequest<bool>;

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordService _passwordService;

    public ResetPasswordCommandHandler(IApplicationDbContext context, IPasswordService passwordService)
    {
        _context = context;
        _passwordService = passwordService;
    }

    public async Task<bool> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.UsuarioId == request.UsuarioId, cancellationToken)
            ?? throw new KeyNotFoundException($"Usuario {request.UsuarioId} no encontrado");

        // Admin resetea contraseña (no requiere contraseña actual)
        usuario.PasswordHash = _passwordService.HashPassword(request.Dto.NewPassword);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}