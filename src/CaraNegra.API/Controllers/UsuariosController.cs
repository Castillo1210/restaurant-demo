using CaraNegra.Application.Auth.Pin;
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
        var result = await _mediator.Send(new GetUsuarioByIdQuery(id));
        return Ok(result);
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
        var result = await _mediator.Send(new UpdateUsuarioCommand(id, dto));
        return Ok(result);
    }

    /// <summary>
    /// Desactiva un usuario (soft delete)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _mediator.Send(new DeleteUsuarioCommand(id));
        return Ok(new { mensaje = "Usuario desactivado correctamente." });
    }

    /// <summary>
    /// Asigna o cambia el PIN de 4 dígitos de un MOZO o COCINA (solo ADMIN). Desbloquea el PIN.
    /// </summary>
    [HttpPut("{id}/pin")]
    public async Task<IActionResult> AsignarPin(int id, [FromBody] AsignarPinDto dto)
    {
        await _mediator.Send(new AsignarPinCommand(id, dto));
        return Ok(new { mensaje = "PIN guardado." });
    }

    /// <summary>Quita el PIN: el usuario vuelve a ingresar solo con contraseña.</summary>
    [HttpDelete("{id}/pin")]
    public async Task<IActionResult> QuitarPin(int id)
    {
        await _mediator.Send(new QuitarPinCommand(id));
        return Ok(new { mensaje = "PIN eliminado." });
    }

    /// <summary>Desbloquea un PIN bloqueado por intentos fallidos.</summary>
    [HttpPost("{id}/pin/desbloquear")]
    public async Task<IActionResult> DesbloquearPin(int id)
    {
        await _mediator.Send(new DesbloquearPinCommand(id));
        return Ok(new { mensaje = "PIN desbloqueado." });
    }

    /// <summary>
    /// Resetea la contraseña de un usuario (solo ADMIN)
    /// </summary>
    [HttpPost("{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetPasswordDto dto)
    {
        await _mediator.Send(new ResetPasswordCommand(id, dto));
        return Ok(new { mensaje = "Contraseña reseteada correctamente." });
    }
}