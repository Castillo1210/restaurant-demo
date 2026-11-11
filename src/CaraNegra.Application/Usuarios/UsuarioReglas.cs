using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Usuarios;

public static class UsuarioReglas
{
    public const string RolAdmin = "ADMIN";

    /// <summary>
    /// Impide dejar el sistema sin ningún administrador activo (desactivándolo o cambiándole el
    /// rol): sin admin nadie puede volver a entrar a Administrar sin tocar la base de datos a mano.
    /// </summary>
    public static async Task ExigirOtroAdminActivoSiDejaDeSerloAsync(IApplicationDbContext context, Usuario usuario,
        bool seguiraSiendoAdmin, bool seguiraActivo, CancellationToken cancellationToken)
    {
        var esAdminActivo = usuario.EsVerificado
            && await context.Roles.AnyAsync(r => r.RolId == usuario.RolId && r.Nombre == RolAdmin, cancellationToken);
        if (!esAdminActivo || (seguiraSiendoAdmin && seguiraActivo))
            return;

        var hayOtro = await context.Usuarios.AnyAsync(u => u.UsuarioId != usuario.UsuarioId
            && u.EsVerificado && u.Rol.Nombre == RolAdmin, cancellationToken);
        if (!hayOtro)
            throw new InvalidOperationException(
                "Es el único administrador activo: crea o activa otro administrador antes de desactivarlo o cambiarle el rol.");
    }
}
