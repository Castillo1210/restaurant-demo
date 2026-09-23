using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CaraNegra.API.DataSeeding;

/// <summary>
/// Siembra los métodos de pago más comunes en Perú para que el módulo de caja sea
/// utilizable desde el primer arranque, sin depender de que exista un panel de
/// administración (Fase 3/5 todavía no existen). Es idempotente: solo agrega los
/// que falten, nunca duplica ni pisa los que el ADMIN ya haya editado.
/// </summary>
public static class MetodoPagoSeeder
{
    private static readonly string[] MetodosPorDefecto = ["Efectivo", "Tarjeta", "Yape", "Plin", "Transferencia"];

    public static async Task SeedMetodosPagoPorDefectoAsync(IServiceProvider rootServices)
    {
        using var scope = rootServices.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var existentes = await context.MetodosPago
            .Select(m => m.Nombre)
            .ToListAsync();

        var faltantes = MetodosPorDefecto
            .Where(nombre => !existentes.Contains(nombre, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (faltantes.Count == 0)
        {
            return;
        }

        foreach (var nombre in faltantes)
        {
            context.MetodosPago.Add(new MetodoPago { Nombre = nombre, EstaActivo = true });
        }

        await context.SaveChangesAsync();
    }
}
