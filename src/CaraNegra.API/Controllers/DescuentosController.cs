using CaraNegra.Application.Descuentos.Commands;
using CaraNegra.Application.Descuentos.DTOs;
using CaraNegra.Application.Descuentos.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaraNegra.API.Controllers;

/// <summary>
/// Catálogo de descuentos (Fase 7) aplicables a un pedido completo. La lectura está
/// disponible para CAJERO/ADMIN (caja necesita ver los descuentos vigentes al cobrar);
/// crear/editar/desactivar descuentos queda reservado a ADMIN.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/descuentos")]
[ApiVersion("1.0")]
[Authorize(Roles = "CAJERO,ADMIN")]
public class DescuentosController : ControllerBase
{
    private readonly IMediator _mediator;

    public DescuentosController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool soloVigentes = false)
    {
        var result = await _mediator.Send(new GetAllDescuentosQuery(soloVigentes));
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var result = await _mediator.Send(new GetDescuentoByIdQuery(id));
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Create([FromBody] CreateDescuentoDto dto)
    {
        var result = await _mediator.Send(new CreateDescuentoCommand(dto));
        return CreatedAtAction(nameof(GetById), new { id = result.DescuentoId }, result);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateDescuentoDto dto)
    {
        try
        {
            var result = await _mediator.Send(new UpdateDescuentoCommand(id, dto));
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
            await _mediator.Send(new DeleteDescuentoCommand(id));
            return Ok(new { mensaje = "Descuento desactivado correctamente." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
    }
}
