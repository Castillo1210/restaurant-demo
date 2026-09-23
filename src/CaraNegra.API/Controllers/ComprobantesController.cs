using System.Security.Claims;
using CaraNegra.Application.Comprobantes.Commands;
using CaraNegra.Application.Comprobantes.DTOs;
using CaraNegra.Application.Comprobantes.Queries;
using CaraNegra.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaraNegra.API.Controllers;

/// <summary>
/// Comprobantes electrónicos (boleta / factura). DEMO: la emisión es SIMULADA — se generan
/// serie, correlativo, IGV y QR con el formato de SUNAT, pero no se envía nada a SUNAT/OSE.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/comprobantes")]
[ApiVersion("1.0")]
[Authorize(Roles = "CAJERO,ADMIN")]
public class ComprobantesController : ControllerBase
{
    private readonly IMediator _mediator;

    public ComprobantesController(IMediator mediator) => _mediator = mediator;

    /// <summary>Listado de comprobantes por rango de fechas (calendario de Lima) y tipo.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] DateTime? fechaDesde = null,
        [FromQuery] DateTime? fechaHasta = null,
        [FromQuery] TipoComprobante? tipo = null)
    {
        var result = await _mediator.Send(new GetAllComprobantesQuery(fechaDesde, fechaHasta, tipo));
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            return Ok(await _mediator.Send(new GetComprobanteByIdQuery(id)));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
    }

    /// <summary>Comprobante vigente de un pedido (204 si todavía no tiene).</summary>
    [HttpGet("pedido/{pedidoId}")]
    public async Task<IActionResult> GetByPedido(int pedidoId)
    {
        var result = await _mediator.Send(new GetComprobanteByPedidoQuery(pedidoId));
        return result == null ? NoContent() : Ok(result);
    }

    /// <summary>Emite (simula) la boleta o factura de un pedido ya pagado.</summary>
    [HttpPost]
    public async Task<IActionResult> Emitir([FromBody] EmitirComprobanteDto dto)
    {
        try
        {
            var usuarioId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result = await _mediator.Send(new EmitirComprobanteCommand(dto, usuarioId));
            return CreatedAtAction(nameof(GetById), new { id = result.ComprobanteId }, result);
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
