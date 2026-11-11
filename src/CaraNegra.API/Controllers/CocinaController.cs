using CaraNegra.Application.Cocina.Commands;
using CaraNegra.Application.Cocina.DTOs;
using CaraNegra.Application.Cocina.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaraNegra.API.Controllers;

/// <summary>
/// Pantalla de cocina (KDS) en tiempo real. Convive con la comanda impresa: la ticketera sigue
/// imprimiendo cada pedido (ImpresoraCocinaService) y además cocina lo ve y lo marca aquí.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/cocina")]
[ApiVersion("1.0")]
[Authorize(Roles = "COCINA,ADMIN")]
public class CocinaController : ControllerBase
{
    private readonly IMediator _mediator;

    public CocinaController(IMediator mediator) => _mediator = mediator;

    /// <summary>Pedidos activos en cocina (Pendiente, En preparación, Listo).</summary>
    [HttpGet("pedidos")]
    public async Task<IActionResult> GetPedidos()
    {
        return Ok(await _mediator.Send(new GetPedidosCocinaQuery()));
    }

    /// <summary>Cambia el estado de un ítem (Pendiente → EnPreparacion → Listo).</summary>
    [HttpPatch("pedidos/{pedidoId}/items/{detalleId}/estado")]
    public async Task<IActionResult> CambiarEstadoItem(int pedidoId, int detalleId, [FromBody] CambiarEstadoItemCocinaDto dto)
    {
        return Ok(await _mediator.Send(new CambiarEstadoItemCocinaCommand(pedidoId, detalleId, dto.Estado)));
    }

    /// <summary>Mueve el pedido completo a EnPreparacion ("Empezar") o Listo ("Pedido listo").</summary>
    [HttpPatch("pedidos/{pedidoId}/estado")]
    public async Task<IActionResult> CambiarEstadoPedido(int pedidoId, [FromBody] CambiarEstadoPedidoCocinaDto dto)
    {
        return Ok(await _mediator.Send(new CambiarEstadoPedidoCocinaCommand(pedidoId, dto.Estado)));
    }
}
