using CaraNegra.Application.Common;
using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Pedidos.Commands;
using CaraNegra.Application.Pedidos.DTOs;
using CaraNegra.Application.Pedidos.Queries;
using CaraNegra.Domain.Entities;
using CaraNegra.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Pedidos;

/// <summary>Un precio distinto al de la carta en un pedido (auditoría).</summary>
public class CambioPrecioDto
{
    public int CambioPrecioPedidoId { get; set; }
    public int PedidoId { get; set; }
    public int DetallePedidoId { get; set; }
    public string Mesa { get; set; } = string.Empty;
    public string ProductoNombre { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal PrecioAnterior { get; set; }
    public decimal PrecioNuevo { get; set; }
    public string? Motivo { get; set; }
    public string UsuarioNombre { get; set; } = string.Empty;
    public string UsuarioRol { get; set; } = string.Empty;
    public DateTime CambiadoEn { get; set; }

    public static CambioPrecioDto Desde(CambioPrecioPedido c) => new()
    {
        CambioPrecioPedidoId = c.CambioPrecioPedidoId,
        PedidoId = c.PedidoId,
        DetallePedidoId = c.DetallePedidoId,
        Mesa = c.Mesa,
        ProductoNombre = c.ProductoNombre,
        Cantidad = c.Cantidad,
        PrecioAnterior = c.PrecioAnterior,
        PrecioNuevo = c.PrecioNuevo,
        Motivo = c.Motivo,
        UsuarioNombre = c.UsuarioNombre,
        UsuarioRol = c.UsuarioRol,
        CambiadoEn = c.CambiadoEn
    };
}

/// <summary>Reglas comunes del cambio de precio (solo cajero / admin).</summary>
public static class PreciosPedido
{
    public const decimal Maximo = 100_000m;
    public const string SinPermiso = "Solo el cajero o el administrador pueden cambiar precios.";

    public static decimal Validar(decimal precio)
    {
        if (precio < 0 || precio > Maximo)
            throw new InvalidOperationException($"El precio debe estar entre 0 y {Maximo:0.##}");
        return Math.Round(precio, 2, MidpointRounding.AwayFromZero);
    }

    public static string EtiquetaMesa(string? salon, string? numero)
    {
        var prefijo = (salon ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return $"{prefijo ?? "Mesa"} {numero}".Trim();
    }

    public static async Task<Usuario> UsuarioAsync(IApplicationDbContext context, int usuarioId, CancellationToken ct) =>
        await context.Usuarios.AsNoTracking().Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.UsuarioId == usuarioId, ct)
        ?? throw new KeyNotFoundException("Usuario no encontrado");

    public static CambioPrecioPedido Registro(Pedido pedido, Mesa? mesa, DetallePedido detalle, string productoNombre,
        decimal anterior, decimal nuevo, string? motivo, Usuario usuario) => new()
    {
        PedidoId = pedido.PedidoId,
        JornadaId = pedido.JornadaId,
        DetallePedidoId = detalle.DetallePedidoId,
        ProductoNombre = productoNombre,
        Cantidad = detalle.Cantidad,
        PrecioAnterior = anterior,
        PrecioNuevo = nuevo,
        Motivo = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim()[..Math.Min(200, motivo.Trim().Length)],
        Mesa = EtiquetaMesa(mesa?.Salon?.Nombre, mesa?.NumeroMesa),
        UsuarioId = usuario.UsuarioId,
        UsuarioNombre = usuario.NombreCompleto,
        UsuarioRol = usuario.Rol?.Nombre ?? string.Empty,
        CambiadoEn = DateTime.UtcNow
    };
}

// ───────────── Cambiar el precio de una línea ya enviada ─────────────

public record CambiarPrecioDetalleCommand(int PedidoId, int DetallePedidoId, decimal Precio, string? Motivo, int UsuarioId)
    : IRequest<PedidoDto>;

public class CambiarPrecioDetalleCommandHandler : IRequestHandler<CambiarPrecioDetalleCommand, PedidoDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IPedidosHubService _hub;
    private readonly IMediator _mediator;

    public CambiarPrecioDetalleCommandHandler(IApplicationDbContext context, IPedidosHubService hub, IMediator mediator)
    {
        _context = context;
        _hub = hub;
        _mediator = mediator;
    }

    public async Task<PedidoDto> Handle(CambiarPrecioDetalleCommand request, CancellationToken cancellationToken)
    {
        var nuevo = PreciosPedido.Validar(request.Precio);

        await using var tx = await _context.IniciarTransaccionAsync(cancellationToken);
        await _context.BloquearPedidoAsync(request.PedidoId, cancellationToken);

        var pedido = await _context.Pedidos
            .Include(p => p.Mesa).ThenInclude(m => m.Salon)
            .Include(p => p.DetallesPedidos).ThenInclude(d => d.Producto)
            .Include(p => p.Pagos)
            .Include(p => p.DetallesDescuentos).ThenInclude(dd => dd.Descuento)
            .FirstOrDefaultAsync(p => p.PedidoId == request.PedidoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Pedido {request.PedidoId} no encontrado");

        if (pedido.EstadoPedido is EstadoPedido.Cancelado or EstadoPedido.Entregado)
            throw new InvalidOperationException("El pedido ya fue cobrado o cancelado: no se puede cambiar el precio");
        if (pedido.Pagos.Any(p => !p.EstaAnulado))
            throw new InvalidOperationException("El pedido ya tiene pagos registrados. Anúlalos primero para cambiar un precio.");

        var detalle = pedido.DetallesPedidos.FirstOrDefault(d => d.DetallePedidoId == request.DetallePedidoId)
            ?? throw new KeyNotFoundException($"El detalle {request.DetallePedidoId} no pertenece al pedido {request.PedidoId}");
        if (detalle.EstadoDetallePedido == EstadoDetallePedido.Cancelado)
            throw new InvalidOperationException("Ese producto está cancelado");

        var anterior = detalle.Monto;
        if (anterior == nuevo)
            return await _mediator.Send(new GetPedidoByIdQuery(pedido.PedidoId), cancellationToken);

        var usuario = await PreciosPedido.UsuarioAsync(_context, request.UsuarioId, cancellationToken);

        detalle.Monto = nuevo;
        pedido.SubTotal += (nuevo - anterior) * detalle.Cantidad;
        if (pedido.SubTotal < 0) pedido.SubTotal = 0;
        pedido.Total = DescuentoCalculator.CalcularTotal(pedido.SubTotal, pedido.DetallesDescuentos.FirstOrDefault()?.Descuento);

        _context.CambiosPrecioPedido.Add(PreciosPedido.Registro(pedido, pedido.Mesa, detalle,
            detalle.Producto?.Nombre ?? "Producto", anterior, nuevo, request.Motivo, usuario));

        await _context.SaveChangesAsync(cancellationToken);
        await tx.ConfirmarAsync(cancellationToken);

        await _hub.NotificarPedidoActualizado(new PedidoActualizadoEvent
        {
            PedidoId = pedido.PedidoId,
            MesaNumero = pedido.Mesa?.NumeroMesa ?? string.Empty,
            SubTotal = pedido.SubTotal,
            Total = pedido.Total
        });

        return await _mediator.Send(new GetPedidoByIdQuery(pedido.PedidoId), cancellationToken);
    }
}

// ───────────── Consultas ─────────────

public record GetCambiosPrecioPedidoQuery(int PedidoId) : IRequest<List<CambioPrecioDto>>;

public class GetCambiosPrecioPedidoQueryHandler : IRequestHandler<GetCambiosPrecioPedidoQuery, List<CambioPrecioDto>>
{
    private readonly IApplicationDbContext _context;

    public GetCambiosPrecioPedidoQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<CambioPrecioDto>> Handle(GetCambiosPrecioPedidoQuery request, CancellationToken cancellationToken)
    {
        var lista = await _context.CambiosPrecioPedido.AsNoTracking()
            .Where(c => c.PedidoId == request.PedidoId)
            .OrderByDescending(c => c.CambiadoEn)
            .ToListAsync(cancellationToken);
        return lista.Select(CambioPrecioDto.Desde).ToList();
    }
}
