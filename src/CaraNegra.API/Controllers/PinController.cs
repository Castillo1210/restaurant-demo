using CaraNegra.Application.Auth.Pin;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CaraNegra.API.Controllers;

/// <summary>
/// Ingreso rápido con PIN de 4 dígitos (MOZO y COCINA) desde dispositivos registrados.
/// El dispositivo se identifica con la cabecera X-Dispositivo-Token (ver DispositivosController).
/// Sin un token válido se responde 403 y el frontend vuelve al login con contraseña.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/auth/pin")]
[ApiVersion("1.0")]
[AllowAnonymous]
[EnableRateLimiting("PinPolicy")]
public class PinController : ControllerBase
{
    private readonly IMediator _mediator;

    public PinController(IMediator mediator) => _mediator = mediator;

    private string? TokenDelDispositivo => Request.Headers[TokenDispositivo.Header].FirstOrDefault();

    /// <summary>Nombres del personal con PIN, para la pantalla de ingreso del dispositivo.</summary>
    [HttpGet("pantalla")]
    public async Task<IActionResult> Pantalla()
    {
        try
        {
            return Ok(await _mediator.Send(new GetPantallaPinQuery(TokenDelDispositivo)));
        }
        catch (DispositivoNoAutorizadoException ex)
        {
            return DispositivoNoAutorizado(ex);
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginPinRequest request)
    {
        try
        {
            return Ok(await _mediator.Send(new LoginConPinCommand(TokenDelDispositivo, request)));
        }
        catch (DispositivoNoAutorizadoException ex)
        {
            return DispositivoNoAutorizado(ex);
        }
    }

    private ObjectResult DispositivoNoAutorizado(Exception ex) =>
        StatusCode(StatusCodes.Status403Forbidden, new { mensaje = ex.Message, codigo = "DISPOSITIVO_NO_AUTORIZADO" });
}
