using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Comprobantes;

internal static class ComprobanteQueries
{
    /// <summary>Comprobantes con todo lo necesario para dibujar el ticket.</summary>
    public static IQueryable<Comprobante> ConDetalle(IApplicationDbContext context) =>
        context.Comprobantes
            .Include(c => c.Usuario)
            .Include(c => c.Pedido).ThenInclude(p => p.Mesa).ThenInclude(m => m.Salon)
            .Include(c => c.Pedido).ThenInclude(p => p.Usuario)
            .Include(c => c.Pedido).ThenInclude(p => p.DetallesPedidos).ThenInclude(d => d.Producto)
            .Include(c => c.Pedido).ThenInclude(p => p.Pagos).ThenInclude(p => p.MetodoPago);
}
