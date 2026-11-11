using System.Security.Claims;
using CaraNegra.Application.Jornadas;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaraNegra.API.Controllers;

/// <summary>
/// Jornada = día operativo: apertura (fondo de caja), movimientos de caja, cierre (con lo
/// registrado), reporte de cierre y pedidos del día. Abren y cierran ADMIN y CAJERO; el historial es solo del ADMIN.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/jornadas")]
[ApiVersion("1.0")]
[Authorize]
public class JornadasController : ControllerBase
{
    private readonly IMediator _mediator;

    public JornadasController(IMediator mediator) => _mediator = mediator;

    private int UsuarioId => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    /// <summary>¿Hay jornada abierta? (todos los roles: el mozo ve el aviso si no la hay)</summary>
    [HttpGet("actual")]
    public async Task<IActionResult> Actual() => Ok(await _mediator.Send(new GetJornadaActualQuery()));

    [HttpPost("abrir")]
    [Authorize(Roles = "ADMIN,CAJERO")]
    public Task<IActionResult> Abrir([FromBody] AbrirJornadaDto dto) =>
        Ejecutar(() => _mediator.Send(new AbrirJornadaCommand(dto, UsuarioId)));

    /// <summary>
    /// Resumen en vivo de la jornada abierta. El CAJERO no ve montos hasta cerrar (arqueo ciego);
    /// el ADMIN ve todo.
    /// </summary>
    [HttpGet("actual/resumen")]
    [Authorize(Roles = "ADMIN,CAJERO")]
    public Task<IActionResult> ResumenActual() =>
        Ejecutar(() => _mediator.Send(new GetResumenJornadaQuery(null, User.IsInRole("ADMIN"))));

    [HttpGet("actual/precierre")]
    [Authorize(Roles = "ADMIN,CAJERO")]
    public Task<IActionResult> PreCierre() => Ejecutar(() => _mediator.Send(new GetPreCierreQuery()));

    [HttpPost("actual/cerrar")]
    [Authorize(Roles = "ADMIN,CAJERO")]
    public Task<IActionResult> Cerrar([FromBody] CerrarJornadaDto dto) =>
        Ejecutar(() => _mediator.Send(new CerrarJornadaCommand(dto, UsuarioId)));

    [HttpPost("actual/movimientos")]
    [Authorize(Roles = "ADMIN,CAJERO")]
    public Task<IActionResult> RegistrarMovimiento([FromBody] RegistrarMovimientoDto dto) =>
        Ejecutar(() => _mediator.Send(new RegistrarMovimientoCommand(dto, UsuarioId)));

    [HttpPost("movimientos/{id:int}/anular")]
    [Authorize(Roles = "ADMIN,CAJERO")]
    public Task<IActionResult> AnularMovimiento(int id) =>
        Ejecutar(() => _mediator.Send(new AnularMovimientoCommand(id, UsuarioId)));

    /// <summary>
    /// "Pedidos del día": todos los pedidos de la jornada abierta (o la última). El ADMIN puede
    /// pedir otra jornada con ?jornadaId=.
    /// </summary>
    [HttpGet("pedidos")]
    [Authorize(Roles = "ADMIN,CAJERO")]
    public Task<IActionResult> Pedidos([FromQuery] int? jornadaId) =>
        Ejecutar(() => _mediator.Send(new GetPedidosJornadaQuery(jornadaId, User.IsInRole("ADMIN"))));

    /// <summary>Historial de jornadas (ADMIN).</summary>
    [HttpGet]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Historial([FromQuery] int cantidad = 60) =>
        Ok(await _mediator.Send(new GetJornadasQuery(cantidad)));

    /// <summary>Reporte de una jornada (ADMIN).</summary>
    [HttpGet("{id:int}/resumen")]
    [Authorize(Roles = "ADMIN")]
    public Task<IActionResult> Resumen(int id) =>
        Ejecutar(() => _mediator.Send(new GetResumenJornadaQuery(id, true)));

    private async Task<IActionResult> Ejecutar<T>(Func<Task<T>> accion)
    {
        return Ok(await accion());
    }
}
