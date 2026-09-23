using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CaraNegra.Infrastructure.Data;

public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        // La cadena de conexión NO se hardcodea en el código fuente. Defina la variable de
        // entorno antes de ejecutar comandos de 'dotnet ef', por ejemplo:
        //   setx RESTAURANT_DB_CONNECTION "Server=localhost;Port=3306;Database=restaurant_demo;User=root;Password=SU_PASSWORD;"
        // (o el equivalente 'export' en Linux/macOS). También se acepta ConnectionStrings__DefaultConnection.
        //
        // IMPORTANTE: este proyecto (restaurant) es una copia de cara_negra. A propósito ya NO se lee
        // CARANEGRA_DB_CONNECTION: así un 'dotnet ef database update' de la demo nunca puede tocar por
        // accidente la base de datos real de Cara Negra.
        var connectionString =
            Environment.GetEnvironmentVariable("RESTAURANT_DB_CONNECTION")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? throw new InvalidOperationException(
                "No se encontró una cadena de conexión para las migraciones de EF Core. " +
                "Defina la variable de entorno RESTAURANT_DB_CONNECTION (o ConnectionStrings__DefaultConnection) " +
                "apuntando a la base de datos de la demo (p.ej. restaurant_demo) antes de ejecutar " +
                "'dotnet ef migrations add' / 'dotnet ef database update'.");

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

        optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}