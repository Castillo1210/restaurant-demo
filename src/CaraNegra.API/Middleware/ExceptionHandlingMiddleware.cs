using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CaraNegra.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() 
            ?? Guid.NewGuid().ToString();
        
        context.Response.Headers["X-Correlation-ID"] = correlationId;

        using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                // Las reglas de negocio ("la mesa ya tiene un pedido", "pedido no encontrado"…) no son
                // fallas del sistema: van al log como aviso (Warning), no como error. Lo inesperado, como error.
                if (EsErrorEsperado(ex))
                    _logger.LogWarning(ex, "Solicitud rechazada: {Mensaje}. Path: {Path}, Method: {Method}",
                        ex.Message, context.Request.Path, context.Request.Method);
                else
                    _logger.LogError(ex, "Unhandled exception occurred. Path: {Path}, Method: {Method}",
                        context.Request.Path, context.Request.Method);

                await HandleExceptionAsync(context, ex, correlationId);
            }
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception, string correlationId)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, title, detail) = Clasificar(exception);

        context.Response.StatusCode = (int)statusCode;

        var json = JsonSerializer.Serialize(CrearCuerpo(statusCode, title, detail, correlationId), new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }

    /// <summary>Código HTTP, título y mensaje para el usuario según el tipo de excepción.</summary>
    public static (HttpStatusCode Status, string Title, string Detail) Clasificar(Exception exception) =>
        exception switch
        {
            _ when EsEsperaDeBloqueo(exception) => (HttpStatusCode.Conflict, "Operación en curso",
                "Otra persona está trabajando sobre este mismo pedido o mesa en este momento. Espera unos segundos y vuelve a intentarlo."),
            KeyNotFoundException => (HttpStatusCode.NotFound, "No encontrado", exception.Message),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "No autorizado", exception.Message),
            InvalidOperationException => (HttpStatusCode.BadRequest, "Operación inválida", exception.Message),
            ArgumentException => (HttpStatusCode.BadRequest, "Argumento inválido", exception.Message),
            _ => (HttpStatusCode.InternalServerError, "Error interno del servidor", "Ha ocurrido un error inesperado.")
        };

    /// <summary>Cuerpo JSON de error. Incluye "mensaje" (lo que el frontend lee primero).</summary>
    public static object CrearCuerpo(HttpStatusCode statusCode, string title, string detail, string? correlationId) => new
    {
        correlationId,
        title,
        detail,
        mensaje = detail,
        status = (int)statusCode,
        timestamp = DateTime.UtcNow
    };

    /// <summary>Errores de reglas de negocio o de datos del usuario (4xx), no fallas del sistema.</summary>
    public static bool EsErrorEsperado(Exception exception) =>
        EsEsperaDeBloqueo(exception)
        || exception is KeyNotFoundException or UnauthorizedAccessException or InvalidOperationException or ArgumentException;

    /// <summary>
    /// MySQL: 1205 = se agotó la espera de un bloqueo, 1213 = interbloqueo. Pasa solo si dos
    /// operaciones sobre el mismo pedido/mesa se cruzan (ver IApplicationDbContext); no es un error
    /// del sistema, basta con reintentar.
    /// </summary>
    private static bool EsEsperaDeBloqueo(Exception exception)
    {
        for (var ex = exception; ex != null; ex = ex.InnerException)
        {
            if (ex is MySqlConnector.MySqlException mysql && (mysql.Number == 1205 || mysql.Number == 1213))
                return true;
        }
        return false;
    }
}

public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}