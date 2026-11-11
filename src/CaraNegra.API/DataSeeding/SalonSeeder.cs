using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CaraNegra.API.DataSeeding;

/// <summary>
/// Las mesas creadas antes de que existieran los salones quedan con SalonId = null. Al arrancar,
/// si hay alguna así, se crea (si no existe) un "Salón principal" y se les asigna, para que
/// ninguna mesa quede fuera del mapa del mozo. Si no hay mesas huérfanas no hace nada.
/// </summary>
public static class SalonSeeder
{
    private const string NombreSalonPorDefecto = "Salón principal";

    public static async Task AsignarMesasSinSalonAsync(IServiceProvider rootServices)
    {
        using var scope = rootServices.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var mesasSinSalon = await context.Mesas.Where(m => m.SalonId == null).ToListAsync();
        if (mesasSinSalon.Count == 0)
            return;

        var salon = await context.Salones.FirstOrDefaultAsync(s => s.Nombre == NombreSalonPorDefecto);
        if (salon is null)
        {
            salon = new Salon { Nombre = NombreSalonPorDefecto, Orden = 0, EstaActivo = true };
            context.Salones.Add(salon);
            await context.SaveChangesAsync();
        }

        foreach (var mesa in mesasSinSalon)
            mesa.SalonId = salon.SalonId;

        await context.SaveChangesAsync();
    }
}
