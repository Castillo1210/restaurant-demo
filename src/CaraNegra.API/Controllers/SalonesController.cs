using CaraNegra.Application.Salones.Commands;
using CaraNegra.Application.Salones.DTOs;
using CaraNegra.Application.Salones.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaraNegra.API.Controllers;

/// <summary>
/// Salones / áreas del restaurante (Terraza, Jardín, Salón principal...). Cada mesa pertenece
/// a un salón. Lectura para todo el personal; crear/editar/desactivar solo ADMIN.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/salones")]
[ApiVersion("1.0")]
[Authorize(Roles = "MOZO,CAJERO,ADMIN")]
public class SalonesController : ControllerBase
{
    private readonly IMediator _mediator;

    public SalonesController(IMediator mediator) => _mediator = mediator;

    /// <param name="soloActivos">Si true, solo los salones activos (lo que usa el mapa del mozo).</param>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool soloActivos = false)
    {
        return Ok(await _mediator.Send(new GetAllSalonesQuery(soloActivos)));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        return Ok(await _mediator.Send(new GetSalonByIdQuery(id)));
    }

    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Create([FromBody] CreateSalonDto dto)
    {
        var result = await _mediator.Send(new CreateSalonCommand(dto));
        return CreatedAtAction(nameof(GetById), new { id = result.SalonId }, result);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateSalonDto dto)
    {
        return Ok(await _mediator.Send(new UpdateSalonCommand(id, dto)));
    }

    /// <summary>Desactiva el salón (soft delete). Se reactiva editándolo.</summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Delete(int id)
    {
        await _mediator.Send(new DeleteSalonCommand(id));
        return Ok(new { mensaje = "Salón desactivado correctamente." });
    }
}
