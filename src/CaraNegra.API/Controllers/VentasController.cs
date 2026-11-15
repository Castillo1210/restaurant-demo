using System.Security.Claims;
using CaraNegra.Application.Pedidos.Queries;
using CaraNegra.Application.Ventas;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaraNegra.API.Controllers;

/// <summary>
/// Venta de mostrador ("Venta rápida"): entradas de la piscina, ropa de baño, flotadores… en un
/// solo paso, sin mesa ni cocina. La TAQUILLA vende en la caja de la piscina; el CAJERO y el ADMIN,
/// en la del restaurante.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/ventas")]
[ApiVersion("1.0")]
[Authorize(Roles = "TAQUILLA,CAJERO,ADMIN")]
public class VentasController : ControllerBase
{
    private readonly IMediator _mediator;

    public VentasController(IMediator mediator) => _mediator = mediator;

    /// <summary>Registra la venta ya cobrada (pedido + pagos). La boleta se emite después en POST /comprobantes.</summary>
    [HttpPost("directa")]
    public async Task<IActionResult> Directa([FromBody] VentaDirectaDto dto)
    {
        var usuarioId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var caja = Mostrador.CajaDelRol(User.IsInRole(Mostrador.RolTaquilla));
        return Ok(await _mediator.Send(new VentaDirectaCommand(dto, usuarioId, caja)));
    }

    /// <summary>
    /// Detalle de una venta de mostrador (para "Ventas de hoy"). La taquilla no tiene acceso a
    /// /pedidos: aquí solo ve pedidos de la mesa de mostrador, nunca los del salón.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var pedido = await _mediator.Send(new GetPedidoByIdQuery(id));
        return pedido.MesaNumero == Mostrador.NumeroMesa ? Ok(pedido) : NotFound();
    }
}
