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
                _logger.LogError(ex, "Unhandled exception occurred. Path: {Path}, Method: {Method}", 
                    context.Request.Path, context.Request.Method);
                
                await HandleExceptionAsync(context, ex, correlationId);
            }
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception, string correlationId)
    {
        context.Response.ContentType = "application/json";
        
        var (statusCode, title, detail) = exception switch
        {
            KeyNotFoundException => (HttpStatusCode.NotFound, "No encontrado", exception.Message),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "No autorizado", exception.Message),
            InvalidOperationException => (HttpStatusCode.BadRequest, "Operación inválida", exception.Message),
            ArgumentException => (HttpStatusCode.BadRequest, "Argumento inválido", exception.Message),
            _ => (HttpStatusCode.InternalServerError, "Error interno del servidor", "Ha ocurrido un error inesperado.")
        };

        context.Response.StatusCode = (int)statusCode;

        var response = new
        {
            correlationId,
            title,
            detail,
            status = (int)statusCode,
            timestamp = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
        });

        await context.Response.WriteAsync(json);
    }
}

public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}