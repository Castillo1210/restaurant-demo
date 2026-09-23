using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace CaraNegra.IntegrationTests;

public class CaraNegraWebApplicationFactory : WebApplicationFactory<Program>
{
    // Program.cs valida JwtSettings:Secret con "builder.Configuration[...]" ANTES de llamar a
    // builder.Build() (falla rápido si falta). WebApplicationFactory.ConfigureWebHost solo puede
    // inyectar configuración/servicios en el momento en que builder.Build() se ejecuta (evento
    // "HostBuilding"), es decir, DESPUÉS de que ese código de Program.cs ya se ejecutó. Por eso
    // ConfigureAppConfiguration de más abajo nunca alcanza a evitar la excepción de arranque.
    // La única config que SÍ está disponible antes de Build() es la que WebApplication.CreateBuilder
    // agrega de forma síncrona por defecto (appsettings.json, variables de entorno...), así que
    // fijamos las variables de entorno aquí, en el constructor estático (se ejecuta una sola vez,
    // antes de que cualquier test de esta clase cree el host).
    static CaraNegraWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("JwtSettings__Secret", "TestSecretKeyForTestingPurposesOnly12345678901234567890");
        Environment.SetEnvironmentVariable("JwtSettings__Issuer", "TestIssuer");
        Environment.SetEnvironmentVariable("JwtSettings__Audience", "TestAudience");
        Environment.SetEnvironmentVariable("JwtSettings__ExpiresInMinutes", "60");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Redundante con las variables de entorno de arriba para el arranque, pero se deja
            // como respaldo explícito por si algo llega a leer IConfiguration ya construido.
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Secret"] = "TestSecretKeyForTestingPurposesOnly12345678901234567890",
                ["JwtSettings:Issuer"] = "TestIssuer",
                ["JwtSettings:Audience"] = "TestAudience",
                ["JwtSettings:ExpiresInMinutes"] = "60"
            }!);
        });

        // No hace falta tocar el registro de DbContextOptions<ApplicationDbContext> aquí:
        // Program.cs ya configura la base de datos en memoria automáticamente cuando el
        // entorno es "Testing" (ver el bloque "if (isTesting) ... UseInMemoryDatabase(...)").
        // Quitar ese descriptor sin volver a agregarlo (como hacía esto antes) dejaba a
        // ApplicationDbContext sin ninguna opción de conexión registrada en absoluto.
    }
}