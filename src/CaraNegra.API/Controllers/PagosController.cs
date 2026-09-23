using System.Security.Claims;
using CaraNegra.Application.Pagos.Commands;
using CaraNegra.Application.Pagos.DTOs;
using CaraNegra.Application.Pagos.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaraNegra.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/pagos")]
[ApiVersion("1.0")]
[Authorize(Roles = "CAJERO,ADMIN")]
public class PagosController : ControllerBase
{
    private readonly IMediator _mediator;

    public PagosController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Lista paginada de pagos con filtros opcionales
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DateTime? fechaDesde = null,
        [FromQuery] DateTime? fechaHasta = null,
        [FromQuery] int? metodoPagoId = null)
    {
        var result = await _mediator.Send(new GetAllPagosQuery(page, pageSize, fechaDesde, fechaHasta, metodoPagoId));
        return Ok(result);
    }

    /// <summary>
    /// Obtiene un pago por su ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var result = await _mediator.Send(new GetPagoByIdQuery(id));
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
    }

    /// <summary>
    /// Obtiene todos los pagos de un pedido específico
    /// </summary>
    [HttpGet("pedido/{pedidoId}")]
    public async Task<IActionResult> GetByPedido(int pedidoId)
    {
        var result = await _mediator.Send(new GetPagosByPedidoQuery(pedidoId));
        return Ok(result);
    }

    /// <summary>
    /// Registra un nuevo pago (soporta pago mixto: múltiples pagos por pedido)
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePagoDto dto)
    {
        try
        {
            var result = await _mediator.Send(new CreatePagoCommand(dto));
            return CreatedAtAction(nameof(GetById), new { id = result.PagoId }, result);
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

    /// <summary>
    /// Anula un pago (reversa) - recalcula estado del pedido y mesa
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Anular(int id, [FromBody] AnularPagoRequest request)
    {
        try
        {
            var usuarioId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result = await _mediator.Send(new AnularPagoCommand(id, request.Motivo, usuarioId));
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

public record AnularPagoRequest(string Motivo);