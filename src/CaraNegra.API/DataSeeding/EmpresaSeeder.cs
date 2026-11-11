using CaraNegra.Application.Common;
using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CaraNegra.API.DataSeeding;

/// <summary>
/// Primera vez que arranca el sistema (tabla ConfiguracionEmpresa vacía): copia los datos de la
/// sección "Empresa" de appsettings.json a la base de datos. Desde ese momento los datos se
/// editan en Admin → Datos de la empresa y appsettings.json ya no se usa para esto.
/// También precarga la caché (así la comanda impresa ya tiene el nombre comercial).
/// </summary>
public static class EmpresaSeeder
{
    public static async Task SeedAsync(IServiceProvider rootServices)
    {
        using var scope = rootServices.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var porDefecto = scope.ServiceProvider.GetRequiredService<CaraNegra.Application.Empresa.DatosEmpresaCache>().PorDefecto;

        var existe = await context.ConfiguracionesEmpresa.AnyAsync(e => e.ConfiguracionEmpresaId == ConfiguracionEmpresa.IdUnico);
        if (!existe)
        {
            context.ConfiguracionesEmpresa.Add(new ConfiguracionEmpresa
            {
                ConfiguracionEmpresaId = ConfiguracionEmpresa.IdUnico,
                Ruc = porDefecto.Ruc,
                RazonSocial = porDefecto.RazonSocial,
                NombreComercial = porDefecto.NombreComercial,
                Direccion = porDefecto.Direccion,
                Telefono = string.IsNullOrWhiteSpace(porDefecto.Telefono) ? null : porDefecto.Telefono,
                SerieBoleta = porDefecto.SerieBoleta,
                SerieFactura = porDefecto.SerieFactura,
                IgvPorcentaje = porDefecto.IgvPorcentaje,
                MensajePie = "¡Gracias por su preferencia!"
            });
            await context.SaveChangesAsync();
        }

        await scope.ServiceProvider.GetRequiredService<IDatosEmpresaProvider>().ObtenerAsync();
    }
}
