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
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Polly;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Enrichers.CorrelationId;
using System.Threading.RateLimiting;

// Instalado como servicio de Windows (ver INSTALACION_WINDOWS.md) el directorio de trabajo es
// C:\Windows\System32: se cambia a la carpeta del programa para que logs/ y appsettings*.json
// se lean y escriban junto al .exe.
var esServicioWindows = WindowsServiceHelpers.IsWindowsService();
if (esServicioWindows)
{
    Directory.SetCurrentDirectory(AppContext.BaseDirectory);
}

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
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args,
        ContentRootPath = esServicioWindows ? AppContext.BaseDirectory : default
    });

    // Servicio de Windows "RestaurantePOS": arranca solo con el equipo (no hace falta iniciar sesión).
    builder.Host.UseWindowsService(options => options.ServiceName = "RestaurantePOS");
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
    // Errores de negocio (404/400…) → respuesta JSON uniforme, sin try/catch en cada acción.
    builder.Services.AddControllers(options => options.Filters.Add<CaraNegra.API.Middleware.ErroresEsperadosFilter>())
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

    // Límites configurables (sección "RateLimiting"). En Development se suben porque en la demo
    // varios equipos (laptop, celulares, tablet de cocina) salen a internet con la misma IP pública.
    var limitePorMinuto = builder.Configuration.GetValue("RateLimiting:PermitLimitPorMinuto", 100);
    var loginsPorMinuto = builder.Configuration.GetValue("RateLimiting:LoginsPorMinuto", 5);

    builder.Services.AddRateLimiter(options =>
    {
        // ClienteIp.Obtener: detrás del proxy de Vite / túnel (cloudflared), todas las peticiones
        // llegan desde 127.0.0.1; en ese caso se usa la IP real que viene en X-Forwarded-For.
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: ClienteIp.Obtener(context),
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = limitePorMinuto,
                    Window = TimeSpan.FromMinutes(1)
                }));

        // Ingreso con contraseña: cupo POR EQUIPO (IP). Antes era uno solo para todo el local, y si
        // al abrir entraban 6 personas en el mismo minuto, la sexta recibía "demasiadas solicitudes".
        options.AddPolicy("AuthPolicy", context =>
            RateLimitPartition.GetFixedWindowLimiter("ip:" + ClienteIp.Obtener(context), _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = loginsPorMinuto,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 2
            }));

        // Ingreso con PIN: cupo POR DISPOSITIVO (como el de contraseña, por equipo), para que varios
        // mozos puedan entrar y "cambiar de mozo" en hora punta. La fuerza bruta la frena el
        // bloqueo del PIN por usuario (PinReglas) y el requisito de dispositivo registrado.
        options.AddPolicy("PinPolicy", context =>
        {
            var dispositivo = context.Request.Headers[CaraNegra.Application.Auth.Pin.TokenDispositivo.Header].ToString();
            var clave = dispositivo.Length is > 0 and <= 200
                ? "disp:" + dispositivo
                : "ip:" + ClienteIp.Obtener(context);
            return RateLimitPartition.GetFixedWindowLimiter(clave, _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1)
            });
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
    // Envíos del mozo ya procesados (para que un reintento por Wi-Fi no duplique platos).
    builder.Services.AddSingleton<CaraNegra.Application.Pedidos.OperacionesRecientes>();

    // Datos de la empresa: se guardan en la BD (Admin → Datos de la empresa) y se cachean en
    // memoria. La sección "Empresa" de appsettings solo se usa como valor inicial (EmpresaSeeder).
    // DEMO: la facturación es simulada, no hay conexión con SUNAT/OSE.
    builder.Services.AddSingleton(new CaraNegra.Application.Empresa.DatosEmpresaCache(
        builder.Configuration.GetSection("Empresa").Get<CaraNegra.Application.Common.DatosEmpresa>()
        ?? new CaraNegra.Application.Common.DatosEmpresa()));
    builder.Services.AddScoped<IDatosEmpresaProvider, CaraNegra.Application.Empresa.DatosEmpresaProvider>();

    // Consulta de DNI (RENIEC) / RUC (SUNAT) con API externa; ver ConsultaDocumentosOptions.
    builder.Services.Configure<CaraNegra.API.ConsultaDocumentos.ConsultaDocumentosOptions>(
        builder.Configuration.GetSection("ConsultaDocumentos"));
    builder.Services.AddHttpClient(CaraNegra.API.ConsultaDocumentos.ConsultaDocumentoService.HttpClientName);
    builder.Services.AddScoped<IConsultaDocumentoService, CaraNegra.API.ConsultaDocumentos.ConsultaDocumentoService>();

    // Facturación electrónica: "Simulado" (demo) o "ApiSunat" (PSE, sandbox/producción).
    // Ver FacturacionElectronicaOptions. El token va en user-secrets, nunca en appsettings.
    builder.Services.Configure<CaraNegra.API.FacturacionElectronica.FacturacionElectronicaOptions>(
        builder.Configuration.GetSection("FacturacionElectronica"));
    builder.Services.AddHttpClient(CaraNegra.API.FacturacionElectronica.FacturacionElectronicaService.HttpClientName);
    builder.Services.AddScoped<IFacturacionElectronicaService, CaraNegra.API.FacturacionElectronica.FacturacionElectronicaService>();
    if (!builder.Environment.IsEnvironment("Testing"))
        builder.Services.AddHostedService<CaraNegra.API.FacturacionElectronica.ComprobantesPendientesWorker>();

    builder.Services.AddHttpClient("ExternalApi", client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddPolicyHandler(HttpResiliencePolicy.CreateCombinedPolicy());

    builder.Services.AddOpenApi();

    var app = builder.Build();

    app.UseExceptionHandling();
    app.UseResponseCompression();

    // Producción: el frontend compilado (Vite) se publica en wwwroot y lo sirve este mismo
    // servicio, en el mismo puerto que la API (sin Node.js ni CORS). Va antes del rate limiting
    // para que los archivos de la app no gasten el cupo de peticiones por minuto.
    app.UseDefaultFiles();
    app.UseStaticFiles(new StaticFileOptions
    {
        OnPrepareResponse = ctx =>
        {
            var ruta = ctx.Context.Request.Path.Value ?? string.Empty;
            // Los archivos de /assets llevan hash en el nombre: se pueden cachear para siempre.
            // index.html, sw.js y manifest NO, para que una actualización llegue a todos al recargar.
            ctx.Context.Response.Headers.CacheControl = ruta.StartsWith("/assets/", StringComparison.OrdinalIgnoreCase)
                ? "public, max-age=31536000, immutable"
                : "no-cache";
        }
    });
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

    // Rutas de la app React (/dashboard, /caja, /cocina…): si no son de la API ni del hub y
    // existe el build del frontend, se responde index.html y React Router decide la pantalla.
    var indexHtml = Path.Combine(app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"), "index.html");
    if (File.Exists(indexHtml))
    {
        app.MapFallback(async context =>
        {
            if (context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/hubs"))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.Headers.CacheControl = "no-cache";
            await context.Response.SendFileAsync(indexHtml);
        });
    }

    // Crea un ADMIN inicial (contraseña aleatoria, se muestra una sola vez en el log) si la
    // base de datos todavía no tiene ninguno. No corre en "Testing": las pruebas de integración
    // siembran sus propios usuarios de prueba con SeedAdminAsync.
    if (!app.Environment.IsEnvironment("Testing"))
    {
        // Al encender la PC, Windows puede iniciar este servicio antes de que MySQL esté listo:
        // se espera a la base de datos unos segundos en vez de fallar (ver EsperaBaseDeDatos).
        await EsperaBaseDeDatos.EsperarAsync(connectionString!, app.Logger);

        await AdminBootstrapSeeder.SeedDefaultAdminIfNoneExistsAsync(
            app.Services, app.Logger);

        // Siembra Efectivo/Tarjeta/Yape/Plin/Transferencia si no existen, para que
        // caja tenga métodos de pago disponibles desde el primer arranque.
        await MetodoPagoSeeder.SeedMetodosPagoPorDefectoAsync(app.Services);

        // Datos de la empresa: la primera vez se copian de appsettings a la BD.
        await EmpresaSeeder.SeedAsync(app.Services);

        // Mesas creadas antes de existir los salones → "Salón principal".
        await SalonSeeder.AsignarMesasSinSalonAsync(app.Services);
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