using CaraNegra.API.Reportes;
using CaraNegra.Application.Pedidos.Queries;
using CaraNegra.Application.Reportes.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaraNegra.API.Controllers;

/// <summary>
/// Reportes básicos de ventas para el panel de administración (Fase 4). Solo ADMIN:
/// expone montos cobrados y desglose de ventas, información sensible del negocio.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/reportes")]
[ApiVersion("1.0")]
[Authorize(Roles = "ADMIN")]
public class ReportesController : ControllerBase
{
    private readonly IMediator _mediator;

    public ReportesController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Resumen de ventas del rango de fechas indicado: total cobrado, cantidad de pedidos,
    /// ticket promedio y desglose por método de pago.
    /// </summary>
    [HttpGet("resumen-ventas")]
    public async Task<IActionResult> ResumenVentas([FromQuery] DateTime fechaDesde, [FromQuery] DateTime fechaHasta)
    {
        if (fechaDesde > fechaHasta)
        {
            return BadRequest(new { mensaje = "La fecha de inicio no puede ser posterior a la fecha de fin." });
        }

        var result = await _mediator.Send(new GetResumenVentasQuery(fechaDesde, fechaHasta));
        return Ok(result);
    }

    /// <summary>
    /// Productos más vendidos (por cantidad) dentro del rango de fechas indicado.
    /// </summary>
    [HttpGet("productos-mas-vendidos")]
    public async Task<IActionResult> ProductosMasVendidos(
        [FromQuery] DateTime fechaDesde,
        [FromQuery] DateTime fechaHasta,
        [FromQuery] int top = 10)
    {
        if (fechaDesde > fechaHasta)
        {
            return BadRequest(new { mensaje = "La fecha de inicio no puede ser posterior a la fecha de fin." });
        }

        var result = await _mediator.Send(new GetProductosMasVendidosQuery(fechaDesde, fechaHasta, top));
        return Ok(result);
    }

    private const string TipoContenidoXlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>
    /// Exporta a Excel (.xlsx) el resumen de ventas y los productos más vendidos del rango
    /// indicado, en un solo libro con dos hojas.
    /// </summary>
    [HttpGet("exportar")]
    public async Task<IActionResult> ExportarResumenVentas([FromQuery] DateTime fechaDesde, [FromQuery] DateTime fechaHasta)
    {
        if (fechaDesde > fechaHasta)
        {
            return BadRequest(new { mensaje = "La fecha de inicio no puede ser posterior a la fecha de fin." });
        }

        var resumen = await _mediator.Send(new GetResumenVentasQuery(fechaDesde, fechaHasta));
        var productos = await _mediator.Send(new GetProductosMasVendidosQuery(fechaDesde, fechaHasta, 50));

        var bytes = ReportesExcelBuilder.ConstruirResumenVentas(resumen, productos);
        var nombreArchivo = $"reporte-ventas_{fechaDesde:yyyy-MM-dd}_a_{fechaHasta:yyyy-MM-dd}.xlsx";

        return File(bytes, TipoContenidoXlsx, nombreArchivo);
    }

    /// <summary>
    /// Pedidos hechos en una mesa dentro del rango de fechas indicado (rango opcional: si no
    /// se indica, trae todo el historial de esa mesa). Pensado para el detalle "pedidos por
    /// mesa" del panel de Reportes.
    /// </summary>
    [HttpGet("pedidos-por-mesa")]
    public async Task<IActionResult> PedidosPorMesa(
        [FromQuery] int mesaId,
        [FromQuery] DateTime? fechaDesde = null,
        [FromQuery] DateTime? fechaHasta = null)
    {
        var result = await _mediator.Send(new GetAllPedidosQuery(1, 500, null, mesaId, fechaDesde, fechaHasta));
        return Ok(result);
    }

    /// <summary>
    /// Exporta a Excel (.xlsx) el detalle línea por línea de los pedidos de una mesa en el
    /// rango indicado.
    /// </summary>
    [HttpGet("pedidos-por-mesa/exportar")]
    public async Task<IActionResult> ExportarPedidosPorMesa(
        [FromQuery] int mesaId,
        [FromQuery] DateTime? fechaDesde = null,
        [FromQuery] DateTime? fechaHasta = null)
    {
        var result = await _mediator.Send(new GetAllPedidosQuery(1, 500, null, mesaId, fechaDesde, fechaHasta));
        var mesaNumero = result.Items.FirstOrDefault()?.MesaNumero ?? mesaId.ToString();

        var bytes = ReportesExcelBuilder.ConstruirPedidosPorMesa(result.Items, mesaNumero);
        var nombreArchivo = $"pedidos-mesa-{mesaNumero}.xlsx";

        return File(bytes, TipoContenidoXlsx, nombreArchivo);
    }
}
