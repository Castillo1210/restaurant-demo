using CaraNegra.Application.Pedidos.Commands;
using CaraNegra.Application.Pedidos.DTOs;
using CaraNegra.Application.Pedidos.Queries;
using CaraNegra.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaraNegra.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/pedidos")]
[ApiVersion("1.0")]
[Authorize(Roles = "MOZO,CAJERO,ADMIN")]
public class PedidosController : ControllerBase
{
    private readonly IMediator _mediator;

    public PedidosController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Lista paginada de pedidos con filtros opcionales
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] EstadoPedido? estado = null,
        [FromQuery] int? mesaId = null,
        [FromQuery] DateTime? fechaDesde = null,
        [FromQuery] DateTime? fechaHasta = null)
    {
        var result = await _mediator.Send(new GetAllPedidosQuery(page, pageSize, estado, mesaId, fechaDesde, fechaHasta));
        return Ok(result);
    }

    /// <summary>
    /// Obtiene un pedido por su ID, con detalles y pagos
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var result = await _mediator.Send(new GetPedidoByIdQuery(id));
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
    }

    /// <summary>
    /// Crea un nuevo pedido (toma de orden del mozo). Ocupa la mesa automáticamente.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePedidoDto dto)
    {
        try
        {
            var result = await _mediator.Send(new CreatePedidoCommand(dto));
            return CreatedAtAction(nameof(GetById), new { id = result.PedidoId }, result);
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
    /// Actualiza la mesa/usuario de un pedido (solo permitido en estado Pendiente)
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePedidoDto dto)
    {
        try
        {
            var result = await _mediator.Send(new UpdatePedidoCommand(id, dto));
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

    /// <summary>
    /// Cambia el estado del pedido (Pendiente → EnPreparacion → Listo, o Cancelado).
    /// "Entregado" solo se alcanza automáticamente al completar el pago.
    /// </summary>
    [HttpPatch("{id}/estado")]
    public async Task<IActionResult> CambiarEstado(int id, [FromBody] UpdatePedidoEstadoDto dto)
    {
        try
        {
            var result = await _mediator.Send(new UpdatePedidoEstadoCommand(id, dto));
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

    /// <summary>
    /// Agrega un ítem a un pedido existente (Pendiente o En Preparación)
    /// </summary>
    [HttpPost("{id}/detalles")]
    public async Task<IActionResult> AgregarDetalle(int id, [FromBody] CreatePedidoDetalleDto dto)
    {
        try
        {
            var result = await _mediator.Send(new AgregarDetallePedidoCommand(id, dto));
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

    /// <summary>
    /// Reimprime manualmente la comanda de cocina de un pedido (p.ej. si la impresora estaba
    /// apagada o sin papel cuando se tomó el pedido). No falla si la impresora sigue sin
    /// responder: ver IImpresoraCocinaService.
    /// </summary>
    [HttpPost("{id}/reimprimir")]
    public async Task<IActionResult> Reimprimir(int id)
    {
        try
        {
            await _mediator.Send(new ReimprimirComandaCommand(id));
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
    }

    /// <summary>
    /// Previsualiza el texto exacto de la comanda de cocina de un pedido (mismo formato que se
    /// enviaría a la impresora térmica), sin imprimir nada. Útil para ver/mostrar el formato
    /// de la comanda sin depender de tener la impresora física conectada.
    /// </summary>
    [HttpGet("{id}/comanda-preview")]
    public async Task<IActionResult> PrevisualizarComanda(int id)
    {
        try
        {
            var texto = await _mediator.Send(new PrevisualizarComandaQuery(id));
            return Ok(new { texto });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
    }

    /// <summary>
    /// Quita un ítem de un pedido existente (Pendiente o En Preparación)
    /// </summary>
    [HttpDelete("{id}/detalles/{detalleId}")]
    public async Task<IActionResult> EliminarDetalle(int id, int detalleId)
    {
        try
        {
            var result = await _mediator.Send(new EliminarDetallePedidoCommand(id, detalleId));
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

    /// <summary>
    /// Aplica un descuento (Fase 7) a un pedido. Solo antes de registrar cualquier pago.
    /// </summary>
    [HttpPost("{id}/descuento")]
    [Authorize(Roles = "CAJERO,ADMIN")]
    public async Task<IActionResult> AplicarDescuento(int id, [FromBody] AplicarDescuentoDto dto)
    {
        try
        {
            var result = await _mediator.Send(new AplicarDescuentoCommand(id, dto.DescuentoId));
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

    /// <summary>
    /// Quita el descuento aplicado a un pedido (Fase 7), si aún no tiene pagos registrados.
    /// </summary>
    [HttpDelete("{id}/descuento")]
    [Authorize(Roles = "CAJERO,ADMIN")]
    public async Task<IActionResult> QuitarDescuento(int id)
    {
        try
        {
            var result = await _mediator.Send(new QuitarDescuentoCommand(id));
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
