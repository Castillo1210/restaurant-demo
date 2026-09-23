using CaraNegra.Application.Cremas.Commands;
using CaraNegra.Application.Cremas.DTOs;
using CaraNegra.Application.Cremas.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaraNegra.API.Controllers;

/// <summary>
/// Catálogo de cremas/toppings (Fase 8) que se muestran como chips al armar un pedido
/// (ej. Mayonesa, Ketchup, BBQ). La lectura está abierta a cualquier rol autenticado, ya
/// que cualquiera que tome pedidos (mozo/cajero/admin) necesita ver las opciones vigentes;
/// crear/editar/desactivar queda reservado a ADMIN, igual que el resto de catálogos.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/cremas")]
[ApiVersion("1.0")]
[Authorize]
public class CremasController : ControllerBase
{
    private readonly IMediator _mediator;

    public CremasController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool soloActivas = true)
    {
        var result = await _mediator.Send(new GetAllCremasQuery(soloActivas));
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var result = await _mediator.Send(new GetCremaByIdQuery(id));
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Create([FromBody] CreateCremaDto dto)
    {
        var result = await _mediator.Send(new CreateCremaCommand(dto));
        return CreatedAtAction(nameof(GetById), new { id = result.CremaId }, result);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCremaDto dto)
    {
        try
        {
            var result = await _mediator.Send(new UpdateCremaCommand(id, dto));
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _mediator.Send(new DeleteCremaCommand(id));
            return Ok(new { mensaje = "Crema desactivada correctamente." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
    }
}
