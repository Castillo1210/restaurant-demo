using System.Text;
using CaraNegra.API.DataSeeding;
using CaraNegra.API.Hubs;
using CaraNegra.API.Impresion;
using CaraNegra.API.Middleware;
using CaraNegra.Application.Auth.Interfaces;
using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Infrastructure.Data;
using CaraNegra.Infrastructure.Resilience;
using CaraNegra.Infrastructure.Services;
using FluentValidation;
using FluentValidation.AspNetCore;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Polly;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Enrichers.CorrelationId;
using System.Threading.RateLimiting;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithThreadId()
    .Enrich.WithCorrelationId()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/caranegra-.log", 
        rollingInterval: RollingInterval.Day,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    var isTesting = builder.Environment.IsEnvironment("Testing");

    if (!isTesting)
    {
        builder.Host.UseSerilog((context, services, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithEnvironmentName()
            .Enrich.WithThreadId()
            .Enrich.WithCorrelationId()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}")
            .WriteTo.File("logs/caranegra-.log", 
                rollingInterval: RollingInterval.Day,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}"));
    }

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

    if (!isTesting && string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "ConnectionStrings:DefaultConnection no está configurado. En desarrollo, defínalo con " +
            "'dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" \"...\"' desde CaraNegra.API. " +
            "En producción, use la variable de entorno ConnectionStrings__DefaultConnection.");
    }

    if (isTesting)
    {
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase("CaraNegraTestDb")
                .EnableSensitiveDataLogging(true)
                .EnableDetailedErrors());
    }
    else
    {
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseMySql(connectionString!, ServerVersion.AutoDetect(connectionString!))
                .EnableSensitiveDataLogging(builder.Environment.IsDevelopment())
                .EnableDetailedErrors());
    }

    builder.Services.AddMediatR(cfg =>
        cfg.RegisterServicesFromAssembly(
            typeof(CaraNegra.Application.Auth.Commands.LoginCommand).Assembly));

    builder.Services.AddScoped<IJwtService, JwtService>();
    builder.Services.AddScoped<IPasswordService, PasswordService>();
    builder.Services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

    var jwtSecret = builder.Configuration["JwtSettings:Secret"];
    if (string.IsNullOrWhiteSpace(jwtSecret))
    {
        throw new InvalidOperationException(
            "JwtSettings:Secret no está configurado. En desarrollo, defínalo con " +
            "'dotnet user-secrets set \"JwtSettings:Secret\" \"...\"' desde CaraNegra.API. " +
            "En producción, use la variable de entorno JwtSettings__Secret. Nunca lo escriba en appsettings.json.");
    }

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
                ValidAudience = builder.Configuration["JwtSettings:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
            };

            // SignalR no puede enviar el header "Authorization" en la conexión WebSocket
            // (el navegador no lo permite en el handshake), así que el cliente JS manda el
            // JWT como query string "access_token" en su lugar. Sin este bloque, el hub
            // rechazaría toda conexión en tiempo real con 401 aunque el token sea válido.
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    {
                        context.Token = accessToken;
                    }
                    return Task.CompletedTask;
                }
            };
        });

    builder.Services.AddAuthorization();
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            // Los enums (EstadoPedido, EstadoMesa, EstadoDetallePedido, TipoMovimiento...)
            // se serializan como texto ("Pendiente", "Ocupada") en vez de números (0, 1, 2...).
            // Esto evita que el frontend tenga que acoplarse a índices numéricos frágiles,
            // y también permite enviar esos mismos nombres como texto en el body de los
            // requests (el binding de query string ya aceptaba nombres de enum de por sí).
            options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        });
    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddValidatorsFromAssemblyContaining(typeof(CaraNegra.Application.Auth.Commands.LoginCommand));

    builder.Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = ApiVersionReader.Combine(
            new UrlSegmentApiVersionReader(),
            new HeaderApiVersionReader("x-api-version"),
            new QueryStringApiVersionReader("api-version"));
    });

    builder.Services.AddVersionedApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

    builder.Services.AddResponseCompression(options =>
    {
        options.EnableForHttps = true;
        options.Providers.Add<BrotliCompressionProvider>();
        options.Providers.Add<GzipCompressionProvider>();
    });
    builder.Services.Configure<BrotliCompressionProviderOptions>(options => options.Level = System.IO.Compression.CompressionLevel.Fastest);
    builder.Services.Configure<GzipCompressionProviderOptions>(options => options.Level = System.IO.Compression.CompressionLevel.Fastest);

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("DefaultPolicy", policy =>
        {
            var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();

            // AllowCredentials() no puede combinarse con un origen comodín ("*"): ASP.NET Core
            // lanza una excepción en tiempo de ejecución si se intenta. Antes, si Cors:AllowedOrigins
            // no estaba configurado, el código caía a ["*"] + AllowCredentials(), lo que habría roto
            // la aplicación en el primer request CORS con credenciales. Ahora se falla explícitamente
            // al iniciar, con un mensaje claro, en vez de fallar de forma confusa en producción.
            if (allowedOrigins is null || allowedOrigins.Length == 0)
            {
                throw new InvalidOperationException(
                    "Cors:AllowedOrigins no está configurado. Defina la lista de orígenes permitidos " +
                    "en appsettings.json (o variables de entorno) antes de iniciar la aplicación.");
            }

            policy.WithOrigins(allowedOrigins)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        });
    });

    builder.Services.AddRateLimiter(options =>
    {
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1)
                }));

        options.AddFixedWindowLimiter("AuthPolicy", options =>
        {
            options.PermitLimit = 5;
            options.Window = TimeSpan.FromMinutes(1);
            options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            options.QueueLimit = 2;
        });

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = async (context, token) =>
        {
            context.HttpContext.Response.ContentType = "application/json";
            var response = $"{{\"correlationId\":\"\",\"title\":\"Demasiadas solicitudes\",\"detail\":\"Se ha excedido el límite de solicitudes. Intente de nuevo más tarde.\",\"status\":429,\"timestamp\":\"{DateTime.UtcNow:o}\"}}";
            await context.HttpContext.Response.WriteAsync(response, token);
        };
    });

    builder.Services.AddHealthChecks()
        .AddDbContextCheck<ApplicationDbContext>("database")
        .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

    // SignalR para tiempo real
    builder.Services.AddSignalR(options =>
    {
        options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    });

    builder.Services.AddScoped<IPedidosHubService, PedidosHubService>();

    // Ticketera de cocina (Fase 6): deshabilitada por defecto hasta configurar la IP real
    // de la impresora en appsettings.json / variables de entorno (sección "ImpresoraCocina").
    builder.Services.Configure<ImpresoraCocinaOptions>(builder.Configuration.GetSection("ImpresoraCocina"));
    builder.Services.AddSingleton<IImpresoraCocinaService, ImpresoraCocinaService>();

    // Datos del emisor para los comprobantes electrónicos (sección "Empresa" de appsettings).
    // DEMO: la facturación es simulada, no hay conexión con SUNAT/OSE.
    builder.Services.AddSingleton(
        builder.Configuration.GetSection("Empresa").Get<CaraNegra.Application.Common.DatosEmpresa>()
        ?? new CaraNegra.Application.Common.DatosEmpresa());

    builder.Services.AddHttpClient("ExternalApi", client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddPolicyHandler(HttpResiliencePolicy.CreateCombinedPolicy());

    builder.Services.AddOpenApi();

    var app = builder.Build();

    app.UseExceptionHandling();
    app.UseResponseCompression();
    app.UseCors("DefaultPolicy");
    app.UseRateLimiter();
    
    if (!app.Environment.IsEnvironment("Testing"))
    {
        app.UseSerilogRequestLogging(options =>
        {
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("CorrelationId", httpContext.Response.Headers["X-Correlation-ID"].FirstOrDefault());
                diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].FirstOrDefault());
            };
        });
    }

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference(options =>
        {
            options.Title = "Cara Negra API";
        });
    }

    app.UseHttpsRedirection();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHealthChecks("/health");
    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    });
    app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => false
    });

    // SignalR Hub
    app.MapHub<PedidosHub>("/hubs/pedidos");

    // Crea un ADMIN inicial (contraseña aleatoria, se muestra una sola vez en el log) si la
    // base de datos todavía no tiene ninguno. No corre en "Testing": las pruebas de integración
    // siembran sus propios usuarios de prueba con SeedAdminAsync.
    if (!app.Environment.IsEnvironment("Testing"))
    {
        await AdminBootstrapSeeder.SeedDefaultAdminIfNoneExistsAsync(
            app.Services, app.Logger);

        // Siembra Efectivo/Tarjeta/Yape/Plin/Transferencia si no existen, para que
        // caja tenga métodos de pago disponibles desde el primer arranque.
        await MetodoPagoSeeder.SeedMetodosPagoPorDefectoAsync(app.Services);
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}