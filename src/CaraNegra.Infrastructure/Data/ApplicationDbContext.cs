using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Infrastructure.Data;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) {}

    // Registro de las tablas
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Mesa> Mesas => Set<Mesa>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Articulo> Articulos => Set<Articulo>();
    public DbSet<Descuento> Descuentos => Set<Descuento>();
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<DetallePedido> DetallesPedido => Set<DetallePedido>();
    public DbSet<MetodoPago> MetodosPago => Set<MetodoPago>();
    public DbSet<Pago> Pagos => Set<Pago>();
    public DbSet<DetalleDescuento> DetallesDescuento => Set<DetalleDescuento>();
    public DbSet<MovimientoArticulo> MovimientosArticulo => Set<MovimientoArticulo>();
    public DbSet<Crema> Cremas => Set<Crema>();
    public DbSet<Comprobante> Comprobantes => Set<Comprobante>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configuración de precisión para campos de dinero
        modelBuilder.Entity<Producto>().Property(p => p.Precio).HasColumnType("decimal(10,2)");
        modelBuilder.Entity<Articulo>().Property(a => a.Precio).HasColumnType("decimal(10,2)");
        modelBuilder.Entity<Pedido>().Property(p => p.SubTotal).HasColumnType("decimal(10,2)");
        modelBuilder.Entity<Pedido>().Property(p => p.Total).HasColumnType("decimal(10,2)");
        modelBuilder.Entity<DetallePedido>().Property(d => d.Monto).HasColumnType("decimal(10,2)");
        modelBuilder.Entity<Pago>().Property(p => p.Monto).HasColumnType("decimal(10,2)");
        modelBuilder.Entity<Descuento>().Property(d => d.Valor).HasColumnType("decimal(10,2)");

        // Auditoría de anulación de pagos
        modelBuilder.Entity<Pago>().Property(p => p.Referencia).HasMaxLength(100);
        modelBuilder.Entity<Pago>().Property(p => p.MotivoAnulacion).HasMaxLength(250);

        // NumeroMesa es un código (string), no un correlativo numérico — se limita el largo de
        // columna explícitamente porque MySQL no permite un índice único sobre una columna
        // TEXT/LONGTEXT (el tipo que EF usaría por defecto para un string sin límite) sin
        // especificar un largo de clave.
        modelBuilder.Entity<Mesa>().Property(m => m.NumeroMesa).HasMaxLength(20).IsRequired();

        // Índices únicos a nivel de base de datos (antes solo se validaban en la aplicación,
        // lo que permitía condiciones de carrera entre solicitudes concurrentes).
        modelBuilder.Entity<Usuario>().HasIndex(u => u.NombreUsuario).IsUnique();
        modelBuilder.Entity<Mesa>().HasIndex(m => m.NumeroMesa).IsUnique();

        // Comprobantes electrónicos (simulados para la demo)
        modelBuilder.Entity<Comprobante>(e =>
        {
            e.Property(c => c.Serie).HasMaxLength(4).IsRequired();
            e.Property(c => c.ClienteNumeroDocumento).HasMaxLength(15);
            e.Property(c => c.ClienteNombre).HasMaxLength(150).IsRequired();
            e.Property(c => c.ClienteDireccion).HasMaxLength(200);
            e.Property(c => c.HashResumen).HasMaxLength(64);
            e.Property(c => c.OpGravada).HasColumnType("decimal(10,2)");
            e.Property(c => c.Igv).HasColumnType("decimal(10,2)");
            e.Property(c => c.Descuento).HasColumnType("decimal(10,2)");
            e.Property(c => c.Total).HasColumnType("decimal(10,2)");
            e.Property(c => c.TipoComprobante).HasConversion<string>().HasMaxLength(20);
            e.Property(c => c.ClienteTipoDocumento).HasConversion<string>().HasMaxLength(20);
            e.Property(c => c.Estado).HasConversion<string>().HasMaxLength(20);

            // Numeración única por serie (B001-00000001, B001-00000002...).
            e.HasIndex(c => new { c.Serie, c.Correlativo }).IsUnique();
            e.HasIndex(c => c.PedidoId);
            e.HasIndex(c => c.FechaEmision);

            e.HasOne(c => c.Pedido).WithMany(p => p.Comprobantes).HasForeignKey(c => c.PedidoId);
            e.HasOne(c => c.Usuario).WithMany().HasForeignKey(c => c.UsuarioId);
        });

        // Prevenir borrado en cascada
        foreach (var relationship in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            relationship.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }

    // Autocompletado de Fechas de Auditoría
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<CaraNegra.Domain.Common.BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreadoEn = DateTime.UtcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.ActualizadoEn = DateTime.UtcNow;
                    break;
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}