using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CaraNegra.API.Middleware;

/// <summary>
/// Convierte en respuesta HTTP las excepciones de reglas de negocio que lanzan los comandos
/// (KeyNotFoundException → 404, InvalidOperationException → 400, …), para TODOS los controladores.
/// Reemplaza los try/catch que antes se repetían en cada acción.
///
/// Corre dentro de MVC, así que el log de peticiones registra el código real (404, 400) y no un 500.
/// Lo inesperado no se toca aquí: sigue hasta ExceptionHandlingMiddleware (500 + traza en el log).
/// Un controlador puede seguir atrapando un caso especial (p. ej. 503) con su propio catch.
/// </summary>
public sealed class ErroresEsperadosFilter : IExceptionFilter
{
    private readonly ILogger<ErroresEsperadosFilter> _logger;

    public ErroresEsperadosFilter(ILogger<ErroresEsperadosFilter> logger) => _logger = logger;

    public void OnException(ExceptionContext context)
    {
        var ex = context.Exception;
        if (!ExceptionHandlingMiddleware.EsErrorEsperado(ex))
            return;

        var http = context.HttpContext;
        // Con la excepción completa: si alguna resulta ser un error interno (p. ej. de EF), la traza
        // queda en el log para encontrarlo.
        _logger.LogWarning(ex, "Solicitud rechazada: {Mensaje}. Path: {Path}, Method: {Method}",
            ex.Message, http.Request.Path, http.Request.Method);

        var (status, title, detail) = ExceptionHandlingMiddleware.Clasificar(ex);
        var correlationId = http.Response.Headers["X-Correlation-ID"].FirstOrDefault();
        context.Result = new ObjectResult(ExceptionHandlingMiddleware.CrearCuerpo(status, title, detail, correlationId))
        {
            StatusCode = (int)status
        };
        context.ExceptionHandled = true;
    }
}
