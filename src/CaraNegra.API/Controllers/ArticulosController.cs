using System.Security.Claims;
using CaraNegra.Application.Articulos.Commands;
using CaraNegra.Application.Articulos.DTOs;
using CaraNegra.Application.Articulos.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaraNegra.API.Controllers;

/// <summary>
/// Módulo de inventario/stock (Fase 6): Artículo representa un insumo o producto de
/// almacén (no necesariamente lo que se vende en la carta — eso es Producto). Es de uso
/// exclusivo de ADMIN: es información de costos/existencias del negocio, no algo que
/// mozo o caja necesiten ver. El stock nunca se edita directamente (ver UpdateArticuloDto);
/// solo cambia registrando un movimiento, para mantener el historial auditado.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/articulos")]
[ApiVersion("1.0")]
[Authorize(Roles = "ADMIN")]
public class ArticulosController : ControllerBase
{
    private readonly IMediator _mediator;

    public ArticulosController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool soloActivos = true)
    {
        var result = await _mediator.Send(new GetAllArticulosQuery(soloActivos));
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var result = await _mediator.Send(new GetArticuloByIdQuery(id));
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateArticuloDto dto)
    {
        var result = await _mediator.Send(new CreateArticuloCommand(dto));
        return CreatedAtAction(nameof(GetById), new { id = result.ArticuloId }, result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateArticuloDto dto)
    {
        try
        {
            var result = await _mediator.Send(new UpdateArticuloCommand(id, dto));
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _mediator.Send(new DeleteArticuloCommand(id));
            return Ok(new { mensaje = "Artículo desactivado correctamente." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
    }

    /// <summary>
    /// Historial de movimientos de stock del artículo (más reciente primero).
    /// </summary>
    [HttpGet("{id}/movimientos")]
    public async Task<IActionResult> GetMovimientos(int id)
    {
        var result = await _mediator.Send(new GetMovimientosByArticuloQuery(id));
        return Ok(result);
    }

    /// <summary>
    /// Registra un movimiento de stock (Entrada/Salida/Ajuste) y actualiza el stock del artículo.
    /// </summary>
    [HttpPost("{id}/movimientos")]
    public async Task<IActionResult> RegistrarMovimiento(int id, [FromBody] CreateMovimientoArticuloDto dto)
    {
        try
        {
            var usuarioId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result = await _mediator.Send(new RegistrarMovimientoArticuloCommand(id, usuarioId, dto));
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
    }
}
