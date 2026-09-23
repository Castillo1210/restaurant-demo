using CaraNegra.Application.Auth.DTOs;
using CaraNegra.Application.Auth.Interfaces;
using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Auth.Commands;

public record RegisterCommand(RegisterRequest Request) : IRequest<string>;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, string>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordService _passwordService;

    public RegisterCommandHandler(IApplicationDbContext context, IPasswordService passwordService)
    {
        _context = context;
        _passwordService = passwordService;
    }

    public async Task<string> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var existe = await _context.Usuarios.AnyAsync(u => u.NombreUsuario == request.Request.NombreUsuario, cancellationToken);

        if (existe)
        {
            throw new InvalidOperationException("El nombre de usuario ya está en uso.");
        }

        var usuario = new Usuario
        {
            NombreUsuario = request.Request.NombreUsuario,
            NombreCompleto = request.Request.NombreCompleto,
            PasswordHash = _passwordService.HashPassword(request.Request.Password),
            RolId = request.Request.RolId,
            EsVerificado = true
        };

        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync(cancellationToken);

        return "Usuario registrado correctamente";
    }
}