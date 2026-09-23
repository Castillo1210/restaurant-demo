using CaraNegra.Application.MetodosPago.Commands;
using CaraNegra.Application.MetodosPago.DTOs;
using CaraNegra.Application.MetodosPago.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaraNegra.API.Controllers;

/// <summary>
/// Catálogo de métodos de pago (Efectivo, Tarjeta, Yape, etc.) usado por caja al
/// registrar un cobro. La lectura está disponible para CAJERO/ADMIN (quien cobra
/// necesita ver las opciones); crear/editar/desactivar métodos queda reservado a
/// ADMIN, ya que afecta a todo el sistema, no solo a un turno de caja.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/metodos-pago")]
[ApiVersion("1.0")]
[Authorize(Roles = "CAJERO,ADMIN")]
public class MetodosPagoController : ControllerBase
{
    private readonly IMediator _mediator;

    public MetodosPagoController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool soloActivos = true)
    {
        var result = await _mediator.Send(new GetAllMetodosPagoQuery(soloActivos));
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var result = await _mediator.Send(new GetMetodoPagoByIdQuery(id));
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Create([FromBody] CreateMetodoPagoDto dto)
    {
        var result = await _mediator.Send(new CreateMetodoPagoCommand(dto));
        return CreatedAtAction(nameof(GetById), new { id = result.MetodoPagoId }, result);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateMetodoPagoDto dto)
    {
        try
        {
            var result = await _mediator.Send(new UpdateMetodoPagoCommand(id, dto));
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
            await _mediator.Send(new DeleteMetodoPagoCommand(id));
            return Ok(new { mensaje = "Método de pago desactivado correctamente." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
    }
}
