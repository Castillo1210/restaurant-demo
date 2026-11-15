using System.Security.Claims;
using CaraNegra.API.Reportes;
using CaraNegra.Application.Common;
using CaraNegra.Application.Jornadas;
using CaraNegra.Application.Ventas;
using CaraNegra.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaraNegra.API.Controllers;

/// <summary>
/// Jornada = día operativo: apertura (fondo de caja), movimientos de caja, cierre (con lo
/// registrado), reporte de cierre y pedidos del día. Abren y cierran ADMIN y CAJERO (caja del
/// restaurante) y TAQUILLA (caja de la piscina); el historial es solo del ADMIN.
///
/// Cada caja tiene su propia jornada. La caja la decide el rol: TAQUILLA siempre la piscina, CAJERO
/// siempre el restaurante y el ADMIN la que pida con ?caja=Piscina (por defecto, el restaurante).
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/jornadas")]
[ApiVersion("1.0")]
[Authorize]
public class JornadasController : ControllerBase
{
    private readonly IMediator _mediator;

    public JornadasController(IMediator mediator) => _mediator = mediator;

    private const string RolesCaja = "ADMIN,CAJERO,TAQUILLA";

    private int UsuarioId => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    private bool EsAdmin => User.IsInRole("ADMIN");

    /// <summary>Caja con la que trabaja quien llama (el ADMIN puede elegirla).</summary>
    private CajaJornada CajaDe(CajaJornada? pedida) =>
        EsAdmin && pedida.HasValue ? pedida.Value : Mostrador.CajaDelRol(User.IsInRole(Mostrador.RolTaquilla));

    /// <summary>¿Hay jornada abierta? (todos los roles: el mozo ve el aviso si no la hay)</summary>
    [HttpGet("actual")]
    public async Task<IActionResult> Actual([FromQuery] CajaJornada? caja = null) =>
        Ok(await _mediator.Send(new GetJornadaActualQuery(CajaDe(caja))));

    [HttpPost("abrir")]
    [Authorize(Roles = RolesCaja)]
    public Task<IActionResult> Abrir([FromBody] AbrirJornadaDto dto, [FromQuery] CajaJornada? caja = null) =>
        Ejecutar(() => _mediator.Send(new AbrirJornadaCommand(dto, UsuarioId, CajaDe(caja))));

    /// <summary>
    /// Resumen en vivo de la jornada abierta. El CAJERO no ve montos hasta cerrar (arqueo ciego);
    /// el ADMIN ve todo.
    /// </summary>
    [HttpGet("actual/resumen")]
    [Authorize(Roles = RolesCaja)]
    public Task<IActionResult> ResumenActual([FromQuery] CajaJornada? caja = null) =>
        Ejecutar(() => _mediator.Send(new GetResumenJornadaQuery(null, EsAdmin, CajaDe(caja))));

    [HttpGet("actual/precierre")]
    [Authorize(Roles = RolesCaja)]
    public Task<IActionResult> PreCierre([FromQuery] CajaJornada? caja = null) =>
        Ejecutar(() => _mediator.Send(new GetPreCierreQuery(CajaDe(caja))));

    [HttpPost("actual/cerrar")]
    [Authorize(Roles = RolesCaja)]
    public Task<IActionResult> Cerrar([FromBody] CerrarJornadaDto dto, [FromQuery] CajaJornada? caja = null) =>
        Ejecutar(() => _mediator.Send(new CerrarJornadaCommand(dto, UsuarioId, CajaDe(caja))));

    [HttpPost("actual/movimientos")]
    [Authorize(Roles = RolesCaja)]
    public Task<IActionResult> RegistrarMovimiento([FromBody] RegistrarMovimientoDto dto, [FromQuery] CajaJornada? caja = null) =>
        Ejecutar(() => _mediator.Send(new RegistrarMovimientoCommand(dto, UsuarioId, CajaDe(caja))));

    [HttpPost("movimientos/{id:int}/anular")]
    [Authorize(Roles = RolesCaja)]
    public Task<IActionResult> AnularMovimiento(int id) =>
        Ejecutar(() => _mediator.Send(new AnularMovimientoCommand(id, UsuarioId, EsAdmin ? null : CajaDe(null))));

    /// <summary>
    /// "Pedidos del día": todos los pedidos de la jornada abierta (o la última). El ADMIN puede
    /// pedir otra jornada con ?jornadaId= o el historial de un rango con ?fechaDesde=&amp;fechaHasta=
    /// (fechas operativas de las jornadas, p. ej. un mes entero).
    /// </summary>
    [HttpGet("pedidos")]
    [Authorize(Roles = RolesCaja)]
    public Task<IActionResult> Pedidos([FromQuery] int? jornadaId, [FromQuery] DateTime? fechaDesde = null,
        [FromQuery] DateTime? fechaHasta = null, [FromQuery] CajaJornada? caja = null)
    {
        if (fechaDesde is null && fechaHasta is null)
            return Ejecutar(() => _mediator.Send(new GetPedidosJornadaQuery(jornadaId, EsAdmin, CajaDe(caja))));

        // Historial (solo ADMIN): sin ?caja= trae las dos cajas.
        var (desde, hasta) = Rango(fechaDesde, fechaHasta);
        return Ejecutar(() => _mediator.Send(new GetPedidosRangoQuery(desde, hasta, EsAdmin, caja)));
    }

    /// <summary>Exporta a Excel (.xlsx) el historial de pedidos del rango indicado (ADMIN).</summary>
    [HttpGet("pedidos/exportar")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> ExportarPedidos([FromQuery] DateTime? fechaDesde = null, [FromQuery] DateTime? fechaHasta = null,
        [FromQuery] CajaJornada? caja = null)
    {
        var (desde, hasta) = Rango(fechaDesde, fechaHasta);
        var datos = await _mediator.Send(new GetPedidosRangoQuery(desde, hasta, true, caja));
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
