using CaraNegra.Application.Auth.Interfaces;
using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CaraNegra.API.DataSeeding;

/// <summary>
/// Resuelve el problema de "huevo y gallina" del primer arranque: registrar un usuario
/// nuevo requiere estar autenticado como ADMIN (ver AuthController.Register), pero una
/// base de datos recién creada no tiene ningún usuario. Si no existe ningún ADMIN,
/// este seeder crea uno con una contraseña aleatoria y la escribe en el log una sola vez.
/// No hardcodea ninguna contraseña conocida (eso sería un hueco de seguridad: cualquiera
/// podría probar "admin/admin123" contra cualquier instalación de Cara Negra).
/// </summary>
public static class AdminBootstrapSeeder
{
    private const string RolAdmin = "ADMIN";
    private const string RolMozo = "MOZO";
    private const string RolCajero = "CAJERO";
    private const string RolCocina = "COCINA";

    public static async Task SeedDefaultAdminIfNoneExistsAsync(IServiceProvider rootServices, ILogger logger)
    {
        using var scope = rootServices.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();

        var rolAdmin = await EnsureRolAsync(context, RolAdmin, "Administrador del sistema");

        // Se aseguran también los roles operativos: sin ellos, el ADMIN no podría crear
        // personal de mozo/caja desde /auth/register apenas entra por primera vez.
        await EnsureRolAsync(context, RolMozo, "Mesero / toma de pedidos");
        await EnsureRolAsync(context, RolCajero, "Caja / cobros");
        await EnsureRolAsync(context, RolCocina, "Cocina / pantalla de preparación");

        var yaExisteAdmin = await context.Usuarios.AnyAsync(u => u.RolId == rolAdmin.RolId);
        if (yaExisteAdmin)
        {
            return;
        }

        var passwordTemporal = GenerarPasswordAleatoria();

        context.Usuarios.Add(new Usuario
        {
            NombreUsuario = "admin",
            NombreCompleto = "Administrador",
            PasswordHash = passwordService.HashPassword(passwordTemporal),
            RolId = rolAdmin.RolId,
            EsVerificado = true
        });

        await context.SaveChangesAsync();

        logger.LogWarning(
            "=== Se creó un usuario ADMIN inicial porque la base de datos no tenía ninguno === " +
            "Usuario: admin | Contraseña temporal: {PasswordTemporal} | " +
            "Cámbiala apenas ingreses desde tu perfil. Este mensaje solo aparece una vez, la próxima " +
            "vez que arranques el sistema ya no se mostrará porque ya existirá al menos un ADMIN.",
            passwordTemporal);
    }

    private static async Task<Rol> EnsureRolAsync(IApplicationDbContext context, string nombre, string descripcion)
    {
        var rol = await context.Roles.FirstOrDefaultAsync(r => r.Nombre == nombre);
        if (rol is null)
        {
            rol = new Rol { Nombre = nombre, Descripcion = descripcion };
            context.Roles.Add(rol);
            await context.SaveChangesAsync();
        }

        return rol;
    }

    private static string GenerarPasswordAleatoria()
    {
        // Alfabeto sin caracteres visualmente ambiguos (sin 0/O, 1/l/I), para que la
        // contraseña temporal sea fácil de transcribir a mano desde el log si hace falta.
        const string alfabeto = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789!@#$%";
        const int longitud = 14;

        var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(longitud);
        var chars = new char[longitud];
        for (var i = 0; i < longitud; i++)
        {
            chars[i] = alfabeto[bytes[i] % alfabeto.Length];
        }

        return new string(chars);
    }
}
