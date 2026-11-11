using CaraNegra.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Rol> Roles { get; }
    DbSet<Usuario> Usuarios { get; }
    DbSet<Salon> Salones { get; }
    DbSet<Mesa> Mesas { get; }
    DbSet<Categoria> Categorias { get; }
    DbSet<Producto> Productos { get; }
    DbSet<ProductoImagen> ProductoImagenes { get; }
    DbSet<ProductoAnulado> ProductosAnulados { get; }
    DbSet<CambioPrecioPedido> CambiosPrecioPedido { get; }
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
    DbSet<Cliente> Clientes { get; }
    DbSet<ConfiguracionEmpresa> ConfiguracionesEmpresa { get; }
    DbSet<DispositivoAutorizado> DispositivosAutorizados { get; }
    DbSet<Jornada> Jornadas { get; }
    DbSet<JornadaArqueo> JornadaArqueos { get; }
    DbSet<MovimientoCaja> MovimientosCaja { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    // ── Concurrencia (hora punta) ─────────────────────────────────────────────
    // Las operaciones que leen y luego escriben sobre un mismo pedido / mesa / jornada
    // (cobrar, emitir, agregar platos...) se hacen dentro de una transacción y bloquean
    // la fila primero: si llegan dos peticiones a la vez (doble toque, dos cajas, reintento
    // por Wi-Fi lento), la segunda espera a que termine la primera y ve su resultado.
    // Orden de bloqueo para evitar interbloqueos: jornada → pedido → mesa (crear un pedido
    // bloquea solo la mesa, porque el pedido todavía no existe).

    /// <summary>
    /// Inicia una transacción READ COMMITTED (cada consulta ve lo último confirmado).
    /// Descarta lo que el contexto tenía en memoria (p. ej. lo leído por un validador) para
    /// que todo se relea después del bloqueo. Debe ser lo PRIMERO del handler.
    /// Si no se llama a ConfirmarAsync, al liberarse se deshace todo.
    /// </summary>
    Task<ITransaccionBd> IniciarTransaccionAsync(CancellationToken cancellationToken = default);

    /// <summary>Bloquea el pedido (SELECT ... FOR UPDATE) hasta el fin de la transacción.</summary>
    Task BloquearPedidoAsync(int pedidoId, CancellationToken cancellationToken = default);

    /// <summary>Bloquea la mesa hasta el fin de la transacción.</summary>
    Task BloquearMesaAsync(int mesaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bloquea la jornada abierta: compartido para las operaciones del día (varias a la vez),
    /// exclusivo para el cierre (espera a que terminen las que están en curso).
    /// </summary>
    Task BloquearJornadaAbiertaAsync(bool exclusivo, CancellationToken cancellationToken = default);

    /// <summary>Serializa la apertura de jornada (dos usuarios abriendo el día a la vez).</summary>
    Task BloquearAperturaJornadaAsync(CancellationToken cancellationToken = default);
}