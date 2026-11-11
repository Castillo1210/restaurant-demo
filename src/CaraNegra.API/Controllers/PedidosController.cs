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

    private int UsuarioActualId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    /// <summary>Solo el cajero y el admin pueden poner un precio distinto al de la carta.</summary>
    private bool PuedeCambiarPrecio => User.IsInRole("CAJERO") || User.IsInRole("ADMIN");
    // Los pedidos y anulaciones del administrador y del cajero no salen en la ticketera de cocina:
    // su propio equipo imprime la comanda o el aviso en su ticketera (como la boleta).
    private bool ComandaEnCaja => User.IsInRole("ADMIN") || User.IsInRole("CAJERO");

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
        var result = await _mediator.Send(new GetPedidoByIdQuery(id));
        return Ok(result);
    }

    /// <summary>
    /// Crea un nuevo pedido (toma de orden del mozo). Ocupa la mesa automáticamente.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePedidoDto dto)
    {
        var result = await _mediator.Send(new CreatePedidoCommand(dto, UsuarioActualId, PuedeCambiarPrecio, ComandaEnCaja));
        return CreatedAtAction(nameof(GetById), new { id = result.PedidoId }, result);
    }

    /// <summary>
    /// Actualiza la mesa/usuario de un pedido (solo permitido en estado Pendiente)
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePedidoDto dto)
    {
        var result = await _mediator.Send(new UpdatePedidoCommand(id, dto));
        return Ok(result);
    }

    /// <summary>
    /// Cambia el estado del pedido (Pendiente → EnPreparacion → Listo, o Cancelado).
    /// "Entregado" solo se alcanza automáticamente al completar el pago.
    /// </summary>
    [HttpPatch("{id}/estado")]
    public async Task<IActionResult> CambiarEstado(int id, [FromBody] UpdatePedidoEstadoDto dto)
    {
        var result = await _mediator.Send(new UpdatePedidoEstadoCommand(id, dto));
        return Ok(result);
    }

    /// <summary>
    /// Agrega un ítem a un pedido existente (Pendiente, En preparación o Listo)
    /// </summary>
    [HttpPost("{id}/detalles")]
    public async Task<IActionResult> AgregarDetalle(int id, [FromBody] CreatePedidoDetalleDto dto)
    {
        var result = await _mediator.Send(new AgregarDetallePedidoCommand(id, new[] { dto }, UsuarioActualId, PuedeCambiarPrecio,
            ComandaEnCaja: ComandaEnCaja));
        return Ok(result);
    }

    /// <summary>
    /// Agrega VARIOS ítems a un pedido existente en una sola operación (todo o nada) y con una
    /// sola comanda adicional. Es lo que usa el carrito del mozo.
    /// </summary>
    [HttpPost("{id}/detalles/lote")]
    public async Task<IActionResult> AgregarDetalles(int id, [FromBody] AgregarDetallesDto dto)
    {
        var result = await _mediator.Send(new AgregarDetallePedidoCommand(id, dto.Detalles, UsuarioActualId, PuedeCambiarPrecio, dto.Comentario,
            dto.OperacionId, ComandaEnCaja));
        return Ok(result);
    }

    /// <summary>
    /// Reimprime manualmente la comanda de cocina de un pedido (p.ej. si la impresora estaba
    /// apagada o sin papel cuando se tomó el pedido), rotulada "REIMPRESION". Responde 400 con
    /// un mensaje claro si la impresora no responde o el pedido ya se cobró o anuló.
    /// </summary>
    [HttpPost("{id}/reimprimir")]
    public async Task<IActionResult> Reimprimir(int id)
    {
        await _mediator.Send(new ReimprimirComandaCommand(id));
        return NoContent();
    }

    /// <summary>
    /// Previsualiza el texto exacto de la comanda de cocina de un pedido (mismo formato que se
    /// enviaría a la impresora térmica), sin imprimir nada. Útil para ver/mostrar el formato
    /// de la comanda sin depender de tener la impresora física conectada.
    /// </summary>
    [HttpGet("{id}/comanda-preview")]
    public async Task<IActionResult> PrevisualizarComanda(int id)
    {
        var texto = await _mediator.Send(new PrevisualizarComandaQuery(id));
        return Ok(new { texto });
    }

    /// <summary>
    /// Quita (anula) un ítem de un pedido Pendiente o En preparación (Listo: solo caja/admin) y sin pagos: todo, o solo
    /// <paramref name="cantidad"/> unidades. El motivo es obligatorio y queda registrado quién lo
    /// hizo (ver GET {id}/anulados y el reporte de cierre de la jornada).
    /// </summary>
    [HttpDelete("{id}/detalles/{detalleId}")]
    public async Task<IActionResult> EliminarDetalle(int id, int detalleId, [FromQuery] string? motivo, [FromQuery] int? cantidad)
    {
        var usuarioId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        // El rol MOZO solo puede quitar de sus propias mesas (el cajero y el admin, de cualquiera).
        var soloPropios = !User.IsInRole("CAJERO") && !User.IsInRole("ADMIN");
        var result = await _mediator.Send(new EliminarDetallePedidoCommand(id, detalleId, usuarioId, motivo, cantidad, soloPropios,
            ComandaEnCaja));
        return Ok(result);
    }

    /// <summary>
    /// Anula el pedido COMPLETO (sin pagos activos). Motivo obligatorio; cada plato queda registrado
    /// como anulado, cocina recibe "PEDIDO ANULADO" y la mesa queda libre.
    /// El mozo solo puede anular los pedidos de sus mesas.
    /// </summary>
    [HttpPost("{id}/anular")]
    public async Task<IActionResult> Anular(int id, [FromBody] AnularPedidoDto dto)
    {
        var usuarioId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var soloPropios = !User.IsInRole("CAJERO") && !User.IsInRole("ADMIN");
        return Ok(await _mediator.Send(new AnularPedidoCommand(id, usuarioId, dto?.Motivo, soloPropios,
            dto?.AvisarCocina ?? true, ComandaEnCaja)));
    }

    /// <summary>
    /// Cambia el precio de una línea del pedido (solo cajero / admin, sin pagos registrados).
    /// Queda registrado quién, cuándo, de cuánto a cuánto y el motivo.
    /// </summary>
    [HttpPut("{id}/detalles/{detalleId}/precio")]
    [Authorize(Roles = "CAJERO,ADMIN")]
    public async Task<IActionResult> CambiarPrecio(int id, int detalleId, [FromBody] CambiarPrecioDetalleDto dto)
    {
        return Ok(await _mediator.Send(new CaraNegra.Application.Pedidos.CambiarPrecioDetalleCommand(id, detalleId, dto.Precio, dto.Motivo, UsuarioActualId)));
    }

    /// <summary>
    /// Cambia el comentario general del pedido ("todo para llevar"). Vacío lo borra. Si queda un
    /// comentario, cocina recibe un ticket corto con él. El MOZO solo en sus propias mesas.
    /// </summary>
    [HttpPut("{id}/comentario")]
    public async Task<IActionResult> ActualizarComentario(int id, [FromBody] ActualizarComentarioPedidoDto dto)
    {
        var soloPropios = !User.IsInRole("CAJERO") && !User.IsInRole("ADMIN");
        return Ok(await _mediator.Send(new CaraNegra.Application.Pedidos.ActualizarComentarioPedidoCommand(
            id, dto.Comentario, UsuarioActualId, soloPropios)));
    }

    /// <summary>Precios distintos a la carta en este pedido (quién y cuándo).</summary>
    [HttpGet("{id}/cambios-precio")]
    public async Task<IActionResult> GetCambiosPrecio(int id)
    {
        return Ok(await _mediator.Send(new CaraNegra.Application.Pedidos.GetCambiosPrecioPedidoQuery(id)));
    }

    /// <summary>Productos quitados de este pedido: quién, cuándo, cuánto y por qué.</summary>
    [HttpGet("{id}/anulados")]
    public async Task<IActionResult> GetAnulados(int id)
    {
        return Ok(await _mediator.Send(new CaraNegra.Application.Pedidos.GetAnulacionesPedidoQuery(id)));
    }

    /// <summary>
    /// Aplica un descuento (Fase 7) a un pedido. Solo antes de registrar cualquier pago.
    /// </summary>
    [HttpPost("{id}/descuento")]
    [Authorize(Roles = "CAJERO,ADMIN")]
    public async Task<IActionResult> AplicarDescuento(int id, [FromBody] AplicarDescuentoDto dto)
    {
        var result = await _mediator.Send(new AplicarDescuentoCommand(id, dto.DescuentoId));
        return Ok(result);
    }

    /// <summary>
    /// Quita el descuento aplicado a un pedido (Fase 7), si aún no tiene pagos registrados.
    /// </summary>
    [HttpDelete("{id}/descuento")]
    [Authorize(Roles = "CAJERO,ADMIN")]
    public async Task<IActionResult> QuitarDescuento(int id)
    {
        var result = await _mediator.Send(new QuitarDescuentoCommand(id));
        return Ok(result);
    }
}
