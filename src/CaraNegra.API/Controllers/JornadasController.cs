using System.Security.Claims;
using CaraNegra.API.Reportes;
using CaraNegra.Application.Common;
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
    /// pedir otra jornada con ?jornadaId= o el historial de un rango con ?fechaDesde=&amp;fechaHasta=
    /// (fechas operativas de las jornadas, p. ej. un mes entero).
    /// </summary>
    [HttpGet("pedidos")]
    [Authorize(Roles = "ADMIN,CAJERO")]
    public Task<IActionResult> Pedidos([FromQuery] int? jornadaId, [FromQuery] DateTime? fechaDesde = null,
        [FromQuery] DateTime? fechaHasta = null)
    {
        if (fechaDesde is null && fechaHasta is null)
            return Ejecutar(() => _mediator.Send(new GetPedidosJornadaQuery(jornadaId, User.IsInRole("ADMIN"))));

        var (desde, hasta) = Rango(fechaDesde, fechaHasta);
        return Ejecutar(() => _mediator.Send(new GetPedidosRangoQuery(desde, hasta, User.IsInRole("ADMIN"))));
    }

    /// <summary>Exporta a Excel (.xlsx) el historial de pedidos del rango indicado (ADMIN).</summary>
    [HttpGet("pedidos/exportar")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> ExportarPedidos([FromQuery] DateTime? fechaDesde = null, [FromQuery] DateTime? fechaHasta = null)
    {
        var (desde, hasta) = Rango(fechaDesde, fechaHasta);
        var datos = await _mediator.Send(new GetPedidosRangoQuery(desde, hasta, true));
        var bytes = ReportesExcelBuilder.ConstruirHistorialPedidos(datos, desde, hasta);
        var nombre = desde == hasta ? $"pedidos_{desde:yyyy-MM-dd}.xlsx" : $"pedidos_{desde:yyyy-MM-dd}_a_{hasta:yyyy-MM-dd}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombre);
    }

    /// <summary>Completa un rango de fechas: lo que falte se toma de la otra fecha o de hoy (Lima).</summary>
    private static (DateTime Desde, DateTime Hasta) Rango(DateTime? fechaDesde, DateTime? fechaHasta)
    {
        var hoy = PeruDateRangeHelper.ToHoraPeru(DateTime.UtcNow).Date;
        var desde = (fechaDesde ?? fechaHasta ?? hoy).Date;
        var hasta = (fechaHasta ?? fechaDesde ?? hoy).Date;
        return (desde, hasta);
    }

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
