using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Jornadas;
using CaraNegra.Application.Pedidos;
using CaraNegra.Domain.Entities;
using CaraNegra.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Ventas;

// ───────────── DTOs ─────────────

/// <summary>Body de POST /ventas/directa: lo que se vende y cómo paga el cliente.</summary>
public class VentaDirectaDto
{
    public List<VentaDirectaItemDto> Items { get; set; } = new();
    public List<VentaDirectaPagoDto> Pagos { get; set; } = new();

    /// <summary>
    /// Código de este intento de venta (lo genera la pantalla). Si la respuesta se pierde por el
    /// Wi-Fi y se reintenta, se devuelve la misma venta en vez de cobrar dos veces.
    /// </summary>
    public string? OperacionId { get; set; }
}

public class VentaDirectaItemDto
{
    public int ProductoId { get; set; }
    public int Cantidad { get; set; }
}

public class VentaDirectaPagoDto
{
    public int MetodoPagoId { get; set; }
    /// <summary>Lo que se queda en caja con este medio (sin el vuelto).</summary>
    public decimal Monto { get; set; }
    public string? Referencia { get; set; }
}

public class VentaDirectaResultadoDto
{
    public int PedidoId { get; set; }
    public CajaJornada Caja { get; set; }
    public decimal Total { get; set; }
    public DateTime CreadoEn { get; set; }
    public string Vendedor { get; set; } = string.Empty;
    public List<VentaDirectaLineaDto> Lineas { get; set; } = new();
    public List<VentaDirectaPagoResultadoDto> Pagos { get; set; } = new();
}

public class VentaDirectaLineaDto
{
    public string Producto { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Importe { get; set; }
}

public class VentaDirectaPagoResultadoDto
{
    public string MetodoPago { get; set; } = string.Empty;
    public decimal Monto { get; set; }
}

// ───────────── Venta directa (mostrador) ─────────────

/// <summary>
/// Venta de mostrador en UN solo paso (pantalla "Venta rápida"): crea el pedido ya cobrado en la
/// mesa interna de mostrador, con sus pagos, dentro de la jornada abierta de la caja que vende
/// (la taquilla, en la de la piscina; el cajero y el admin, en la del restaurante). No envía nada
/// a cocina y no deja nada abierto. La boleta se emite después con POST /comprobantes.
/// </summary>
public record VentaDirectaCommand(VentaDirectaDto Dto, int UsuarioId, CajaJornada Caja) : IRequest<VentaDirectaResultadoDto>;

public class VentaDirectaCommandHandler : IRequestHandler<VentaDirectaCommand, VentaDirectaResultadoDto>
{
    private const int MaximoLineas = 50;
    private const int MaximoReferencia = 100;

    private readonly IApplicationDbContext _context;
    private readonly IPedidosHubService _hub;
    private readonly OperacionesRecientes _operaciones;

    public VentaDirectaCommandHandler(IApplicationDbContext context, IPedidosHubService hub, OperacionesRecientes operaciones)
    {
        _context = context;
        _hub = hub;
        _operaciones = operaciones;
    }

    public async Task<VentaDirectaResultadoDto> Handle(VentaDirectaCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto ?? new VentaDirectaDto();
        var items = (dto.Items ?? new()).Where(i => i is not null).ToList();
        var pagos = (dto.Pagos ?? new()).Where(p => p is not null).ToList();

        if (items.Count == 0)
            throw new InvalidOperationException("Agrega al menos un producto.");
        if (items.Count > MaximoLineas)
            throw new InvalidOperationException($"Una venta puede tener como máximo {MaximoLineas} productos distintos.");
        if (items.Any(i => i.ProductoId <= 0 || i.Cantidad < 1 || i.Cantidad > 100))
            throw new InvalidOperationException("La cantidad de cada producto debe estar entre 1 y 100.");
        if (pagos.Count == 0)
            throw new InvalidOperationException("Indica cómo paga el cliente.");
        if (pagos.Any(p => p.MetodoPagoId <= 0 || p.Monto <= 0))
            throw new InvalidOperationException("Cada pago debe tener un medio de pago y un monto mayor a cero.");

        // La jornada se bloquea en modo compartido: el cierre de caja no ocurre a mitad de una venta.
        await using var tx = await _context.IniciarTransaccionAsync(cancellationToken);
        await _context.BloquearJornadaAbiertaAsync(exclusivo: false, cancellationToken);

        // Reintento de una venta que ya se grabó (la respuesta se perdió): se devuelve esa misma.
        var claveOperacion = OperacionesRecientes.Clave(dto.OperacionId);
        if (claveOperacion != null && _operaciones.YaProcesada(claveOperacion, out var pedidoPrevioId))
            return await ConstruirResultadoAsync(pedidoPrevioId, request.Caja, cancellationToken);

        var jornada = await JornadaReglas.RequerirAbiertaAsync(_context, request.Caja, cancellationToken);

        var mesa = await _context.Mesas.FirstOrDefaultAsync(m => m.EsMostrador, cancellationToken)
            ?? throw new InvalidOperationException("Falta la mesa interna de mostrador: reinicia el servicio para que se cree.");

        var usuario = await _context.Usuarios.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UsuarioId == request.UsuarioId, cancellationToken)
            ?? throw new KeyNotFoundException("Usuario no encontrado");

        // Una consulta por producto distinto (sin Contains: ver notas de EF en .NET 10).
        var productos = new Dictionary<int, Producto>();
        foreach (var productoId in items.Select(i => i.ProductoId).Distinct())
        {
            var producto = await _context.Productos
                .Include(p => p.Categoria)
                .FirstOrDefaultAsync(p => p.ProductoId == productoId, cancellationToken)
                ?? throw new KeyNotFoundException($"Producto {productoId} no encontrado");
            if (!producto.EstaDisponible)
                throw new InvalidOperationException($"El producto {producto.Nombre} no está disponible");
            // Lo que se prepara en cocina se vende desde las mesas (con su comanda), no aquí.
            if (!Mostrador.EsDeMostrador(producto))
                throw new InvalidOperationException($"{producto.Nombre} no es un producto de mostrador: se vende desde las mesas.");
            productos[productoId] = producto;
        }

        var metodos = new Dictionary<int, MetodoPago>();
        foreach (var metodoId in pagos.Select(p => p.MetodoPagoId).Distinct())
        {
            var metodo = await _context.MetodosPago
                .FirstOrDefaultAsync(m => m.MetodoPagoId == metodoId, cancellationToken)
                ?? throw new KeyNotFoundException($"Medio de pago {metodoId} no encontrado");
            if (!metodo.EstaActivo)
                throw new InvalidOperationException($"El medio de pago {metodo.Nombre} está desactivado");
            metodos[metodoId] = metodo;
        }

        var detalles = items.Select(i => new DetallePedido
        {
            ProductoId = i.ProductoId,
            Cantidad = i.Cantidad,
            Monto = productos[i.ProductoId].Precio,
            // Se entrega en el momento: no pasa por cocina.
            EstadoDetallePedido = EstadoDetallePedido.Entregado
        }).ToList();

        var total = detalles.Sum(d => d.Monto * d.Cantidad);
        var pagado = pagos.Sum(p => Math.Round(p.Monto, 2));
        if (Math.Abs(pagado - total) > 0.01m)
            throw new InvalidOperationException(
                $"Los pagos (S/ {pagado:0.00}) no coinciden con el total de la venta (S/ {total:0.00}).");

        var pedido = new Pedido
        {
            MesaId = mesa.MesaId,
            UsuarioId = usuario.UsuarioId,
            SubTotal = total,
            Total = total,
            EstadoPedido = EstadoPedido.Entregado,
            JornadaId = jornada.JornadaId,
            DetallesPedidos = detalles
        };
        foreach (var p in pagos)
        {
            var referencia = string.IsNullOrWhiteSpace(p.Referencia) ? null : p.Referencia.Trim();
            if (referencia is { Length: > MaximoReferencia }) referencia = referencia[..MaximoReferencia];
            pedido.Pagos.Add(new Pago
            {
                MetodoPagoId = p.MetodoPagoId,
                Monto = Math.Round(p.Monto, 2),
                Referencia = referencia,
                JornadaId = jornada.JornadaId
            });
        }

        _context.Pedidos.Add(pedido);
        await _context.SaveChangesAsync(cancellationToken);

        // Se registra antes de confirmar: un reintento que espera el bloqueo ya la encuentra.
        if (claveOperacion != null) _operaciones.Registrar(claveOperacion, pedido.PedidoId);
        try
        {
            await tx.ConfirmarAsync(cancellationToken);
        }
        catch
        {
            if (claveOperacion != null) _operaciones.Olvidar(claveOperacion);
            throw;
        }

        // Las pantallas que muestran ventas en vivo (Pedidos del día) se actualizan solas.
        await _hub.NotificarPagoRecibido(new PagoRecibidoEvent
        {
            PedidoId = pedido.PedidoId,
            MesaNumero = mesa.NumeroMesa,
            Monto = total,
            MetodoPago = string.Join(" + ", pagos.Select(p => metodos[p.MetodoPagoId].Nombre).Distinct()),
            EsPagoCompleto = true,
            EstadoPedido = EstadoPedido.Entregado.ToString()
        });

        return await ConstruirResultadoAsync(pedido.PedidoId, request.Caja, cancellationToken);
    }

    private async Task<VentaDirectaResultadoDto> ConstruirResultadoAsync(int pedidoId, CajaJornada caja, CancellationToken ct)
    {
        var pedido = await _context.Pedidos
            .AsNoTracking()
            .Include(p => p.Usuario)
            .Include(p => p.DetallesPedidos).ThenInclude(d => d.Producto)
            .Include(p => p.Pagos).ThenInclude(g => g.MetodoPago)
            .FirstAsync(p => p.PedidoId == pedidoId, ct);

        return new VentaDirectaResultadoDto
        {
            PedidoId = pedido.PedidoId,
            Caja = caja,
            Total = pedido.Total,
            CreadoEn = pedido.CreadoEn,
            Vendedor = pedido.Usuario?.NombreCompleto ?? string.Empty,
            Lineas = pedido.DetallesPedidos.Select(d => new VentaDirectaLineaDto
            {
                Producto = d.Producto?.Nombre ?? string.Empty,
                Cantidad = d.Cantidad,
                PrecioUnitario = d.Monto,
                Importe = d.Monto * d.Cantidad
            }).ToList(),
            Pagos = pedido.Pagos.Where(g => !g.EstaAnulado).Select(g => new VentaDirectaPagoResultadoDto
            {
                MetodoPago = g.MetodoPago?.Nombre ?? string.Empty,
                Monto = g.Monto
            }).ToList()
        };
    }
}
