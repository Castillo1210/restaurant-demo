using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Usuarios.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Usuarios.Commands;

public record DeleteUsuarioCommand(int UsuarioId) : IRequest<bool>;

public class DeleteUsuarioCommandHandler : IRequestHandler<DeleteUsuarioCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public DeleteUsuarioCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<bool> Handle(DeleteUsuarioCommand request, CancellationToken cancellationToken)
    {
        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.UsuarioId == request.UsuarioId, cancellationToken)
            ?? throw new KeyNotFoundException($"Usuario {request.UsuarioId} no encontrado");

        // Soft delete: desactivar usuario
        usuario.EsVerificado = false;
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}