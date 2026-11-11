using System.Security.Claims;
using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Comprobantes.Commands;
using CaraNegra.Application.Comprobantes.DTOs;
using CaraNegra.Application.Comprobantes.Queries;
using CaraNegra.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaraNegra.API.Controllers;

/// <summary>
/// Comprobantes electrónicos (boleta / factura). Según FacturacionElectronica:Proveedor la
/// emisión es simulada (demo) o se envía a APISUNAT (sandbox o producción).
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/comprobantes")]
[ApiVersion("1.0")]
[Authorize(Roles = "CAJERO,ADMIN")]
public class ComprobantesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IFacturacionElectronicaService _facturacion;

    public ComprobantesController(IMediator mediator, IFacturacionElectronicaService facturacion)
    {
        _mediator = mediator;
        _facturacion = facturacion;
    }

    /// <summary>Dónde se emiten los comprobantes (para los avisos de caja).</summary>
    [HttpGet("ambiente")]
    public IActionResult Ambiente() => Ok(new { proveedor = _facturacion.Proveedor, ambiente = _facturacion.Ambiente });

    /// <summary>Número que llevará el próximo comprobante de una serie (ej. ?serie=B001 → B001-00000015).</summary>
    [HttpGet("proximo-numero")]
    public async Task<IActionResult> ProximoNumero([FromQuery] string serie) =>
        Ok(await _mediator.Send(new GetProximoNumeroQuery(serie)));

    /// <summary>Comprobantes emitidos hace más de una hora que SUNAT todavía no confirma (aviso en caja).</summary>
    [HttpGet("atrasados")]
    public async Task<IActionResult> Atrasados() => Ok(await _mediator.Send(new GetComprobantesAtrasadosQuery()));

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
        return Ok(await _mediator.Send(new GetComprobanteByIdQuery(id)));
    }

    /// <summary>Comprobante vigente de un pedido (204 si todavía no tiene).</summary>
    [HttpGet("pedido/{pedidoId}")]
    public async Task<IActionResult> GetByPedido(int pedidoId)
    {
        var result = await _mediator.Send(new GetComprobanteByPedidoQuery(pedidoId));
        return result == null ? NoContent() : Ok(result);
    }

    /// <summary>Emite la boleta o factura de un pedido ya pagado (y la envía al PSE si no es simulado).</summary>
    [HttpPost]
    public async Task<IActionResult> Emitir([FromBody] EmitirComprobanteDto dto)
    {
        var usuarioId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _mediator.Send(new EmitirComprobanteCommand(dto, usuarioId));
        return CreatedAtAction(nameof(GetById), new { id = result.ComprobanteId }, result);
    }

    /// <summary>
    /// Reintenta el envío (o consulta el estado) de un comprobante PorEnviar, Pendiente o con
    /// error de envío, con el mismo número.
    /// </summary>
    [HttpPost("{id}/reenviar")]
    public async Task<IActionResult> Reenviar(int id)
    {
        return Ok(await _mediator.Send(new ReenviarComprobanteCommand(id)));
    }
}
