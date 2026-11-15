using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Ventas;
using CaraNegra.Domain.Entities;
using CaraNegra.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CaraNegra.API.DataSeeding;

/// <summary>
/// Crea, si no existe, la mesa interna de mostrador donde quedan las ventas de "Venta rápida"
/// (piscina). No tiene salón, no aparece en el mapa de mesas y nunca queda ocupada.
/// </summary>
public static class MostradorSeeder
{
    public static async Task AsegurarMesaMostradorAsync(IServiceProvider rootServices)
    {
        using var scope = rootServices.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        if (await context.Mesas.AnyAsync(m => m.EsMostrador))
            return;

        context.Mesas.Add(new Mesa
        {
            NumeroMesa = Mostrador.NumeroMesa,
            Estado = EstadoMesa.Disponible,
            EsMostrador = true
        });
        await context.SaveChangesAsync();
    }
}
