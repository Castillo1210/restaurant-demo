using CaraNegra.Application.Common;
using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Reportes.DTOs;
using CaraNegra.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Reportes.Queries;

public record GetProductosMasVendidosQuery(DateTime FechaDesde, DateTime FechaHasta, int Top = 10)
    : IRequest<List<ProductoMasVendidoDto>>;

public class GetProductosMasVendidosQueryHandler : IRequestHandler<GetProductosMasVendidosQuery, List<ProductoMasVendidoDto>>
{
    private readonly IApplicationDbContext _context;

    public GetProductosMasVendidosQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<ProductoMasVendidoDto>> Handle(GetProductosMasVendidosQuery request, CancellationToken cancellationToken)
    {
        if (request.FechaDesde > request.FechaHasta)
        {
            throw new InvalidOperationException("La fecha de inicio no puede ser posterior a la fecha de fin.");
        }

        // Ver PeruDateRangeHelper: CreadoEn está en UTC, hay que convertir el rango de calendario
        // local (Lima, UTC-5) antes de comparar.
        var fechaDesde = PeruDateRangeHelper.InicioDelDiaUtc(request.FechaDesde);
        var fechaHasta = PeruDateRangeHelper.FinDelDiaUtc(request.FechaHasta);
        var top = request.Top > 0 ? request.Top : 10;

        // Se excluyen los ítems y pedidos cancelados: un producto que se pidió pero nunca
        // se preparó/cobró no debería contar como "vendido".
        var detalles = _context.DetallesPedido
            .Include(d => d.Producto)
                .ThenInclude(p => p.Categoria)
            .Include(d => d.Pedido)
            .Where(d => d.EstadoDetallePedido != EstadoDetallePedido.Cancelado
                && d.Pedido.EstadoPedido != EstadoPedido.Cancelado
                && d.Pedido.CreadoEn >= fechaDesde && d.Pedido.CreadoEn <= fechaHasta);

        var resultado = await detalles
            .GroupBy(d => new { d.ProductoId, d.Producto.Nombre, CategoriaNombre = d.Producto.Categoria.Nombre })
            .Select(g => new ProductoMasVendidoDto
            {
                ProductoId = g.Key.ProductoId,
                ProductoNombre = g.Key.Nombre,
                CategoriaNombre = g.Key.CategoriaNombre,
                CantidadVendida = g.Sum(d => d.Cantidad),
                TotalVendido = g.Sum(d => d.Cantidad * d.Monto)
            })
            .OrderByDescending(p => p.CantidadVendida)
            .Take(top)
            .ToListAsync(cancellationToken);

        return resultado;
    }
}
