using System.Security.Claims;
using CaraNegra.Application.Auth.Pin;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaraNegra.API.Controllers;

/// <summary>
/// Celulares / tablets habilitados para el ingreso con PIN (solo ADMIN).
/// El registro se hace DESDE el propio dispositivo: el ADMIN entra con su contraseña en ese
/// equipo, pulsa "Registrar este dispositivo" y el token que devuelve este endpoint se guarda
/// en el navegador. El token se muestra una sola vez; aquí solo queda su hash.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/dispositivos")]
[ApiVersion("1.0")]
[Authorize(Roles = "ADMIN")]
public class DispositivosController : ControllerBase
{
    private readonly IMediator _mediator;

    public DispositivosController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _mediator.Send(new GetDispositivosQuery()));

    [HttpPost]
    public async Task<IActionResult> Registrar([FromBody] RegistrarDispositivoDto dto)
    {
        var registradoPor = User.FindFirst(ClaimTypes.GivenName)?.Value ?? User.Identity?.Name;
        return Ok(await _mediator.Send(new RegistrarDispositivoCommand(dto, registradoPor)));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Revocar(int id)
    {
        await _mediator.Send(new RevocarDispositivoCommand(id));
        return Ok(new { mensaje = "Dispositivo revocado: ya no puede ingresar con PIN." });
    }
}
