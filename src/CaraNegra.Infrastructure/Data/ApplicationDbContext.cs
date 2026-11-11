using CaraNegra.Application.Common.Interfaces;
using System.Data;
using CaraNegra.Domain.Entities;
using CaraNegra.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Infrastructure.Data;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) {}

    // Registro de las tablas
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Salon> Salones => Set<Salon>();
    public DbSet<Mesa> Mesas => Set<Mesa>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<ProductoImagen> ProductoImagenes => Set<ProductoImagen>();
    public DbSet<ProductoAnulado> ProductosAnulados => Set<ProductoAnulado>();
    public DbSet<CambioPrecioPedido> CambiosPrecioPedido => Set<CambioPrecioPedido>();
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
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<ConfiguracionEmpresa> ConfiguracionesEmpresa => Set<ConfiguracionEmpresa>();
    public DbSet<DispositivoAutorizado> DispositivosAutorizados => Set<DispositivoAutorizado>();
    public DbSet<Jornada> Jornadas => Set<Jornada>();
    public DbSet<JornadaArqueo> JornadaArqueos => Set<JornadaArqueo>();
    public DbSet<MovimientoCaja> MovimientosCaja => Set<MovimientoCaja>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configuración de precisión para campos de dinero
        modelBuilder.Entity<Producto>().Property(p => p.Precio).HasColumnType("decimal(10,2)");

        // Comentario general del pedido ("todo para llevar"); ver ComentarioPedido.Maximo.
        modelBuilder.Entity<Pedido>().Property(p => p.Comentario).HasMaxLength(300);

        // Auditoría de productos quitados de un pedido (quién, cuándo, por qué).
        modelBuilder.Entity<ProductoAnulado>(e =>
        {
            e.ToTable("ProductosAnulados");
            e.Property(a => a.ProductoNombre).HasMaxLength(150).IsRequired();
            e.Property(a => a.PrecioUnitario).HasColumnType("decimal(10,2)");
            e.Property(a => a.Importe).HasColumnType("decimal(10,2)");
            e.Property(a => a.EstadoItem).HasMaxLength(20).IsRequired();
            e.Property(a => a.Motivo).HasMaxLength(200).IsRequired();
            e.Property(a => a.Mesa).HasMaxLength(60).IsRequired();
            e.Property(a => a.UsuarioNombre).HasMaxLength(100).IsRequired();
            e.Property(a => a.UsuarioRol).HasMaxLength(20).IsRequired();
            e.HasOne(a => a.Pedido).WithMany().HasForeignKey(a => a.PedidoId);
            e.HasIndex(a => a.PedidoId);
            e.HasIndex(a => a.JornadaId);
        });

        // Auditoría de precios distintos a la carta (solo cajero / admin).
        modelBuilder.Entity<CambioPrecioPedido>(e =>
        {
            e.ToTable("CambiosPrecioPedido");
            e.Property(c => c.ProductoNombre).HasMaxLength(150).IsRequired();
            e.Property(c => c.PrecioAnterior).HasColumnType("decimal(10,2)");
            e.Property(c => c.PrecioNuevo).HasColumnType("decimal(10,2)");
            e.Property(c => c.Motivo).HasMaxLength(200);
            e.Property(c => c.Mesa).HasMaxLength(60).IsRequired();
            e.Property(c => c.UsuarioNombre).HasMaxLength(100).IsRequired();
            e.Property(c => c.UsuarioRol).HasMaxLength(20).IsRequired();
            e.HasOne(c => c.Pedido).WithMany().HasForeignKey(c => c.PedidoId);
            e.HasIndex(c => c.PedidoId);
            e.HasIndex(c => c.JornadaId);
        });

        // Foto del producto: tabla aparte (1 a 1) para que listar la carta no cargue las imágenes.
        modelBuilder.Entity<ProductoImagen>(e =>
        {
            e.ToTable("ProductoImagenes");
            e.HasKey(i => i.ProductoId);
            e.Property(i => i.ProductoId).ValueGeneratedNever();
            e.Property(i => i.Datos).HasColumnType("mediumblob").IsRequired();
            e.Property(i => i.TipoContenido).HasMaxLength(30).IsRequired();
            e.HasOne(i => i.Producto).WithOne().HasForeignKey<ProductoImagen>(i => i.ProductoId);
        });
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
        modelBuilder.Entity<Usuario>().Property(u => u.PinHash).HasMaxLength(100);

        // Jornadas (día operativo) con su arqueo y movimientos de caja.
        modelBuilder.Entity<Jornada>(e =>
        {
            e.ToTable("Jornadas");
            e.Property(j => j.FechaOperativa).HasColumnType("date");
            e.Property(j => j.Estado).HasConversion<string>().HasMaxLength(10);
            e.Property(j => j.AbiertaPor).HasMaxLength(100).IsRequired();
            e.Property(j => j.CerradaPor).HasMaxLength(100);
            e.Property(j => j.ObservacionApertura).HasMaxLength(300);
            e.Property(j => j.ObservacionCierre).HasMaxLength(500);
            e.Property(j => j.FondoInicial).HasColumnType("decimal(10,2)");
            e.Property(j => j.TotalVentas).HasColumnType("decimal(10,2)");
            e.Property(j => j.TotalIngresos).HasColumnType("decimal(10,2)");
            e.Property(j => j.TotalEgresos).HasColumnType("decimal(10,2)");
            e.Property(j => j.EfectivoEsperado).HasColumnType("decimal(10,2)");
            e.Property(j => j.EfectivoContado).HasColumnType("decimal(10,2)");
            e.Property(j => j.DiferenciaTotal).HasColumnType("decimal(10,2)");
            e.HasIndex(j => j.Estado);
            e.HasIndex(j => j.FechaOperativa);
            e.HasMany(j => j.Arqueos).WithOne(a => a.Jornada).HasForeignKey(a => a.JornadaId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(j => j.Movimientos).WithOne(m => m.Jornada).HasForeignKey(m => m.JornadaId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<JornadaArqueo>(e =>
        {
            e.ToTable("JornadaArqueos");
            e.Property(a => a.MetodoPago).HasMaxLength(50).IsRequired();
            e.Property(a => a.Ventas).HasColumnType("decimal(10,2)");
            e.Property(a => a.Esperado).HasColumnType("decimal(10,2)");
            e.Property(a => a.Contado).HasColumnType("decimal(10,2)");
            e.Property(a => a.Diferencia).HasColumnType("decimal(10,2)");
        });

        modelBuilder.Entity<MovimientoCaja>(e =>
        {
            e.ToTable("MovimientosCaja");
            e.Property(m => m.Tipo).HasConversion<string>().HasMaxLength(10);
            e.Property(m => m.Monto).HasColumnType("decimal(10,2)");
            e.Property(m => m.Motivo).HasMaxLength(200).IsRequired();
            e.Property(m => m.RegistradoPor).HasMaxLength(100).IsRequired();
            e.Property(m => m.AnuladoPor).HasMaxLength(100);
        });

        // Pedidos y pagos quedan amarrados a la jornada en la que se registraron.
        modelBuilder.Entity<Pedido>().HasOne<Jornada>().WithMany().HasForeignKey(p => p.JornadaId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Pedido>().HasIndex(p => p.JornadaId);
        modelBuilder.Entity<Pago>().HasOne<Jornada>().WithMany().HasForeignKey(p => p.JornadaId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Pago>().HasIndex(p => p.JornadaId);

        // Celulares / tablets del restaurante habilitados para el ingreso con PIN.
        modelBuilder.Entity<DispositivoAutorizado>(e =>
        {
            e.ToTable("DispositivosAutorizados");
            e.Property(d => d.Nombre).HasMaxLength(60).IsRequired();
            e.Property(d => d.TokenHash).HasMaxLength(64).IsRequired();
            e.Property(d => d.RegistradoPor).HasMaxLength(100);
            e.HasIndex(d => d.TokenHash).IsUnique();
        });

        // Numeración de mesas POR SALÓN: el mismo número puede repetirse en salones distintos.
        modelBuilder.Entity<Mesa>().HasIndex(m => new { m.SalonId, m.NumeroMesa }).IsUnique();

        // Salones / áreas del restaurante
        modelBuilder.Entity<Salon>(e =>
        {
            e.Property(s => s.Nombre).HasMaxLength(50).IsRequired();
            e.Property(s => s.Descripcion).HasMaxLength(200);
            e.HasIndex(s => s.Nombre).IsUnique();
            e.HasMany(s => s.Mesas).WithOne(m => m.Salon).HasForeignKey(m => m.SalonId);
        });

        // Comprobantes electrónicos (simulados o enviados a un PSE como APISUNAT)
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
            e.Property(c => c.Ambiente).HasMaxLength(12).HasDefaultValue("SIMULADO");
            e.Property(c => c.MensajeSunat).HasMaxLength(500);
            e.Property(c => c.XmlUrl).HasMaxLength(300);
            e.Property(c => c.CdrUrl).HasMaxLength(300);
            e.Property(c => c.PdfUrl).HasMaxLength(300);
            e.HasIndex(c => c.Estado);

            // Numeración única por serie (B001-00000001, B001-00000002...).
            e.HasIndex(c => new { c.Serie, c.Correlativo }).IsUnique();
            e.HasIndex(c => c.PedidoId);
            e.HasIndex(c => c.FechaEmision);

            e.HasOne(c => c.Pedido).WithMany(p => p.Comprobantes).HasForeignKey(c => c.PedidoId);
            e.HasOne(c => c.Usuario).WithMany().HasForeignKey(c => c.UsuarioId);
        });

        // Clientes (caché local de la consulta DNI/RUC): un registro por documento.
        modelBuilder.Entity<Cliente>(e =>
        {
            e.Property(c => c.TipoDocumento).HasConversion<string>().HasMaxLength(20);
            e.Property(c => c.NumeroDocumento).HasMaxLength(15).IsRequired();
            e.Property(c => c.Nombre).HasMaxLength(200).IsRequired();
            e.Property(c => c.Direccion).HasMaxLength(300);
            e.HasIndex(c => new { c.TipoDocumento, c.NumeroDocumento }).IsUnique();
        });

        // Datos de la empresa: tabla de una sola fila (Id = 1), editable desde Admin.
        modelBuilder.Entity<ConfiguracionEmpresa>(e =>
        {
            e.ToTable("ConfiguracionEmpresa");
            e.Property(c => c.ConfiguracionEmpresaId).ValueGeneratedNever();
            e.Property(c => c.Ruc).HasMaxLength(11).IsRequired();
            e.Property(c => c.RazonSocial).HasMaxLength(200).IsRequired();
            e.Property(c => c.NombreComercial).HasMaxLength(100).IsRequired();
            e.Property(c => c.Direccion).HasMaxLength(250).IsRequired();
            e.Property(c => c.Telefono).HasMaxLength(30);
            e.Property(c => c.Email).HasMaxLength(100);
            e.Property(c => c.SerieBoleta).HasMaxLength(4).IsRequired();
            e.Property(c => c.SerieFactura).HasMaxLength(4).IsRequired();
            e.Property(c => c.IgvPorcentaje).HasColumnType("decimal(5,2)");
            e.Property(c => c.MensajePie).HasMaxLength(200);
            e.Property(c => c.LogoBase64).HasColumnType("longtext");
        });

        // Snapshot del emisor en cada comprobante
        modelBuilder.Entity<Comprobante>(e =>
        {
            e.Property(c => c.EmisorRuc).HasMaxLength(11);
            e.Property(c => c.EmisorRazonSocial).HasMaxLength(200);
            e.Property(c => c.EmisorNombreComercial).HasMaxLength(100);
            e.Property(c => c.EmisorDireccion).HasMaxLength(250);
            e.Property(c => c.IgvPorcentaje).HasColumnType("decimal(5,2)");
        });

        // Prevenir borrado en cascada
        foreach (var relationship in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            relationship.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }

    // ── Concurrencia: transacción + bloqueo de filas (ver IApplicationDbContext) ──

    public async Task<ITransaccionBd> IniciarTransaccionAsync(CancellationToken cancellationToken = default)
    {
        // Ya hay una transacción en curso (llamada anidada): se usa esa.
        if (!Database.IsRelational() || Database.CurrentTransaction != null)
            return TransaccionBd.Ninguna;

        // Lo leído antes (validadores) se descarta: después del bloqueo todo se relee fresco.
        ChangeTracker.Clear();
        var tx = await Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            // Si otra operación retiene el bloqueo, esperar como máximo 15 s (no 50 s) y avisar.
            await Database.ExecuteSqlRawAsync("SET SESSION innodb_lock_wait_timeout = 15", cancellationToken);
        }
        catch
        {
            await tx.DisposeAsync();
            throw;
        }
        return new TransaccionBd(tx);
    }

    public Task BloquearPedidoAsync(int pedidoId, CancellationToken cancellationToken = default) =>
        BloquearAsync($"SELECT PedidoId FROM Pedidos WHERE PedidoId = {pedidoId} FOR UPDATE", cancellationToken);

    public Task BloquearMesaAsync(int mesaId, CancellationToken cancellationToken = default) =>
        BloquearAsync($"SELECT MesaId FROM Mesas WHERE MesaId = {mesaId} FOR UPDATE", cancellationToken);

    public Task BloquearJornadaAbiertaAsync(bool exclusivo, CancellationToken cancellationToken = default)
    {
        var abierta = nameof(EstadoJornada.Abierta);
        return exclusivo
            ? BloquearAsync($"SELECT JornadaId FROM Jornadas WHERE Estado = {abierta} FOR UPDATE", cancellationToken)
            : BloquearAsync($"SELECT JornadaId FROM Jornadas WHERE Estado = {abierta} LOCK IN SHARE MODE", cancellationToken);
    }

    // La configuración de la empresa es una sola fila que siempre existe: sirve de "candado"
    // para que dos usuarios no abran dos jornadas al mismo tiempo.
    public Task BloquearAperturaJornadaAsync(CancellationToken cancellationToken = default) =>
        BloquearAsync($"SELECT ConfiguracionEmpresaId FROM ConfiguracionEmpresa ORDER BY ConfiguracionEmpresaId LIMIT 1 FOR UPDATE", cancellationToken);

    private async Task BloquearAsync(FormattableString sql, CancellationToken cancellationToken)
    {
        if (!Database.IsRelational()) return; // pruebas en memoria
        if (Database.CurrentTransaction == null)
            throw new InvalidOperationException("El bloqueo de filas requiere una transacción (IniciarTransaccionAsync).");
        await Database.ExecuteSqlInterpolatedAsync(sql, cancellationToken);
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