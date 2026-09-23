using CaraNegra.Application.Usuarios.Commands;
using CaraNegra.Application.Usuarios.DTOs;
using CaraNegra.Application.Usuarios.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaraNegra.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/usuarios")]
[ApiVersion("1.0")]
[Authorize(Roles = "ADMIN")]
public class UsuariosController : ControllerBase
{
    private readonly IMediator _mediator;

    public UsuariosController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Lista paginada de usuarios con búsqueda opcional
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null)
    {
        var result = await _mediator.Send(new GetAllUsuariosQuery(page, pageSize, search));
        return Ok(result);
    }

    /// <summary>
    /// Obtiene usuarios por rol
    /// </summary>
    [HttpGet("por-rol/{rolId}")]
    public async Task<IActionResult> GetByRol(int rolId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetUsuariosByRolQuery(rolId, page, pageSize));
        return Ok(result);
    }

    /// <summary>
    /// Obtiene un usuario por ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var result = await _mediator.Send(new GetUsuarioByIdQuery(id));
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
    }

    /// <summary>
    /// Crea un nuevo usuario (solo ADMIN)
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUsuarioDto dto)
    {
        var result = await _mediator.Send(new CreateUsuarioCommand(dto));
        return CreatedAtAction(nameof(GetById), new { id = result.UsuarioId }, result);
    }

    /// <summary>
    /// Actualiza un usuario existente
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUsuarioDto dto)
    {
        try
        {
            var result = await _mediator.Send(new UpdateUsuarioCommand(id, dto));
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
    }

    /// <summary>
    /// Desactiva un usuario (soft delete)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _mediator.Send(new DeleteUsuarioCommand(id));
            return Ok(new { mensaje = "Usuario desactivado correctamente." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
    }

    /// <summary>
    /// Cambia la contraseña del usuario autenticado
    /// </summary>
    [HttpPost("{id}/cambiar-password")]
    [Authorize(Roles = "ADMIN,CAJERO,MOZO,COCINA")] // Permitir a todos los roles cambiar su propia contraseña
    public async Task<IActionResult> ChangePassword(int id, [FromBody] ChangePasswordDto dto)
    {
        // Validar que el usuario solo pueda cambiar su propia contraseña (excepto ADMIN)
        if (!User.IsInRole("ADMIN") && int.Parse(User.FindFirst("usuarioId")?.Value ?? "0") != id)
        {
            return Forbid();
        }

        try
        {
            await _mediator.Send(new ChangePasswordCommand(id, dto));
            return Ok(new { mensaje = "Contraseña cambiada correctamente." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
    }

    /// <summary>
    /// Resetea la contraseña de un usuario (solo ADMIN)
    /// </summary>
    [HttpPost("{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetPasswordDto dto)
    {
        try
        {
            await _mediator.Send(new ResetPasswordCommand(id, dto));
            return Ok(new { mensaje = "Contraseña reseteada correctamente." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
    }
}