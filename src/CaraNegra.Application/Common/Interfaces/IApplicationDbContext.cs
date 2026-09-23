using CaraNegra.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Rol> Roles { get; }
    DbSet<Usuario> Usuarios { get; }
    DbSet<Mesa> Mesas { get; }
    DbSet<Categoria> Categorias { get; }
    DbSet<Producto> Productos { get; }
    DbSet<Articulo> Articulos { get; }
    DbSet<Descuento> Descuentos { get; }
    DbSet<Pedido> Pedidos { get; }
    DbSet<DetallePedido> DetallesPedido { get; }
    DbSet<MetodoPago> MetodosPago { get; }
    DbSet<Pago> Pagos { get; }
    DbSet<DetalleDescuento> DetallesDescuento { get; }
    DbSet<MovimientoArticulo> MovimientosArticulo { get; }
    DbSet<Crema> Cremas { get; }
    DbSet<Comprobante> Comprobantes { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}