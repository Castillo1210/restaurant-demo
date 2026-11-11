using CaraNegra.Application.Usuarios.Commands;
using CaraNegra.Application.Usuarios.DTOs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaraNegra.API.Controllers;

/// <summary>
/// Lo que cualquier usuario hace con su PROPIA cuenta. Va aparte de UsuariosController porque
/// aquel exige ADMIN a nivel de clase, y en ASP.NET los permisos de clase y de acción se suman:
/// con la acción allí, un cajero o un mozo recibía 403 al cambiar su contraseña.
/// La ruta es la misma de siempre (/usuarios/{id}/cambiar-password).
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/usuarios")]
[ApiVersion("1.0")]
[Authorize]
public class MiCuentaController : ControllerBase
{
    private readonly IMediator _mediator;

    public MiCuentaController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Cambia la contraseña del usuario autenticado (el ADMIN puede cambiar la de cualquiera).
    /// </summary>
    [HttpPost("{id}/cambiar-password")]
    public async Task<IActionResult> ChangePassword(int id, [FromBody] ChangePasswordDto dto)
    {
        if (!User.IsInRole("ADMIN") && int.Parse(User.FindFirst("usuarioId")?.Value ?? "0") != id)
        {
            return Forbid();
        }

        try
        {
            await _mediator.Send(new ChangePasswordCommand(id, dto));
            return Ok(new { mensaje = "Contraseña cambiada correctamente." });
        }
        catch (UnauthorizedAccessException ex)
        {
            // Contraseña actual incorrecta: 400 y no 401, porque el frontend cierra la sesión ante un 401.
            return BadRequest(new { mensaje = ex.Message });
        }
    }
}
