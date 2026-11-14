using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Domain.Entities;
using CaraNegra.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Jornadas;

/// <summary>
/// "Pedidos del día": todos los pedidos de una jornada (abiertos, cobrados y anulados) en una
/// lista simple para consultar rápido desde Caja. Sin <paramref name="JornadaId"/> usa la jornada
/// abierta o, si no hay, la última cerrada. Solo el ADMIN puede pedir otra jornada.
/// </summary>
public record GetPedidosJornadaQuery(int? JornadaId, bool EsAdmin) : IRequest<PedidosJornadaDto>;

/// <summary>
/// Historial de pedidos (ADMIN): todos los pedidos de las jornadas cuya fecha operativa está entre
/// <paramref name="FechaDesde"/> y <paramref name="FechaHasta"/>, por ejemplo un mes entero. Se filtra
/// por jornada y no por la hora del pedido, igual que el cierre de caja: lo vendido pasada la
/// medianoche cuenta en el día en que se abrió la jornada.
/// </summary>
public record GetPedidosRangoQuery(DateTime FechaDesde, DateTime FechaHasta, bool EsAdmin) : IRequest<PedidosJornadaDto>;

public class PedidosJornadaDto
{
    /// <summary>La jornada consultada (null en una consulta por rango de fechas).</summary>
    public JornadaDto? Jornada { get; set; }
    /// <summary>Rango consultado (solo en una consulta por rango de fechas).</summary>
    public DateTime? FechaDesde { get; set; }
    public DateTime? FechaHasta { get; set; }
    public int CantidadJornadas { get; set; }
    public List<PedidoJornadaDto> Pedidos { get; set; } = new();
}

public class PedidoJornadaDto
{
    public int PedidoId { get; set; }
    /// <summary>Día de la jornada a la que pertenece el pedido.</summary>
    public DateTime? FechaOperativa { get; set; }
    public string MesaNumero { get; set; } = string.Empty;
    public string SalonNombre { get; set; } = string.Empty;
    public string Mozo { get; set; } = string.Empty;
    public DateTime CreadoEn { get; set; }
    /// <summary>Pendiente, EnPreparacion, Listo, Entregado (cobrado) o Cancelado (anulado).</summary>
    public EstadoPedido Estado { get; set; }
    public decimal Total { get; set; }
    public decimal Pagado { get; set; }
    public int CantidadPlatos { get; set; }
    public string? Comentario { get; set; }
    /// <summary>Medios de pago usados (sin los pagos anulados), p. ej. ["Efectivo", "Yape"].</summary>
    public List<string> MetodosPago { get; set; } = new();
    public ComprobanteResumenDto? Comprobante { get; set; }
}

public class ComprobanteResumenDto
{
    public int ComprobanteId { get; set; }
    public TipoComprobante Tipo { get; set; }
    public string Numero { get; set; } = string.Empty;
    public EstadoComprobante Estado { get; set; }
}

public class GetPedidosJornadaQueryHandler : IRequestHandler<GetPedidosJornadaQuery, PedidosJornadaDto>
{
    private readonly IApplicationDbContext _context;

    public GetPedidosJornadaQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<PedidosJornadaDto> Handle(GetPedidosJornadaQuery request, CancellationToken cancellationToken)
    {
        var jornadas = _context.Jornadas.AsNoTracking();
        var jornada = request.JornadaId is int id
            ? await jornadas.FirstOrDefaultAsync(j => j.JornadaId == id, cancellationToken)
                ?? throw new KeyNotFoundException($"Jornada {id} no encontrada")
            : await jornadas.Where(j => j.Estado == EstadoJornada.Abierta).OrderByDescending(j => j.AbiertaEn).FirstOrDefaultAsync(cancellationToken)
                ?? await jornadas.OrderByDescending(j => j.AbiertaEn).FirstOrDefaultAsync(cancellationToken);

        if (jornada == null) return new PedidosJornadaDto();

        // El cajero solo consulta el día abierto (el historial de jornadas es del administrador).
        if (!request.EsAdmin && request.JornadaId is not null && jornada.Estado != EstadoJornada.Abierta)
            throw new InvalidOperationException("Solo el administrador puede ver los pedidos de jornadas anteriores.");

        var pedidos = await PedidosJornadaListado.ListarAsync(
            _context.Pedidos.Where(p => p.JornadaId == jornada.JornadaId),
            new Dictionary<int, DateTime> { [jornada.JornadaId] = jornada.FechaOperativa },
            cancellationToken);

        return new PedidosJornadaDto
        {
            Jornada = JornadaCalculos.ToDto(jornada),
            CantidadJornadas = 1,
            Pedidos = pedidos
        };
    }
}

public class GetPedidosRangoQueryHandler : IRequestHandler<GetPedidosRangoQuery, PedidosJornadaDto>
{
    // Tope para que nadie pida por error años enteros de una sola vez.
    private const int MaxDias = 366;

    private readonly IApplicationDbContext _context;

    public GetPedidosRangoQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<PedidosJornadaDto> Handle(GetPedidosRangoQuery request, CancellationToken cancellationToken)
    {
        if (!request.EsAdmin)
            throw new InvalidOperationException("Solo el administrador puede ver el historial de pedidos.");

        var desde = request.FechaDesde.Date;
        var hasta = request.FechaHasta.Date;
        if (desde > hasta)
            throw new ArgumentException("La fecha de inicio no puede ser posterior a la fecha de fin.");
        if ((hasta - desde).TotalDays >= MaxDias)
            throw new ArgumentException($"El rango no puede pasar de {MaxDias} días.");

        var finExclusivo = hasta.AddDays(1);
        var fechasJornada = await _context.Jornadas
            .AsNoTracking()
            .Where(j => j.FechaOperativa >= desde && j.FechaOperativa < finExclusivo)
            .Select(j => new { j.JornadaId, j.FechaOperativa })
            .ToDictionaryAsync(j => j.JornadaId, j => j.FechaOperativa, cancellationToken);

        var ids = fechasJornada.Keys.ToList();
        var pedidos = ids.Count == 0
            ? new List<PedidoJornadaDto>()
            : await PedidosJornadaListado.ListarAsync(
                _context.Pedidos.Where(p => p.JornadaId != null && ids.Contains(p.JornadaId.Value)),
                fechasJornada,
                cancellationToken);

        return new PedidosJornadaDto
        {
            FechaDesde = desde,
            FechaHasta = hasta,
            CantidadJornadas = ids.Count,
            Pedidos = pedidos
        };
    }
}

/// <summary>Arma la lista de pedidos (con pagos y comprobante) que comparten los dos handlers de arriba.</summary>
internal static class PedidosJornadaListado
{
    public static async Task<List<PedidoJornadaDto>> ListarAsync(IQueryable<Pedido> consulta,
        IReadOnlyDictionary<int, DateTime> fechasJornada, CancellationToken cancellationToken)
    {
        var pedidos = await consulta
            .AsNoTracking()
            .OrderByDescending(p => p.CreadoEn)
            .Select(p => new
            {
                p.PedidoId,
                p.JornadaId,
                MesaNumero = p.Mesa.NumeroMesa,
                SalonNombre = p.Mesa.Salon != null ? p.Mesa.Salon.Nombre : string.Empty,
                Mozo = p.Usuario.NombreCompleto,
                p.CreadoEn,
                p.EstadoPedido,
                p.Total,
                p.Comentario,
                Platos = p.DetallesPedidos.Where(d => d.EstadoDetallePedido != EstadoDetallePedido.Cancelado).Sum(d => (int?)d.Cantidad) ?? 0,
                PlatosAnulados = p.DetallesPedidos.Sum(d => (int?)d.Cantidad) ?? 0,
                Pagos = p.Pagos.Where(g => !g.EstaAnulado).Select(g => new { g.Monto, Metodo = g.MetodoPago.Nombre }).ToList(),
                Comprobante = p.Comprobantes
                    .Where(c => c.Estado != EstadoComprobante.Anulado)
                    .OrderByDescending(c => c.FechaEmision)
                    .Select(c => new { c.ComprobanteId, c.TipoComprobante, c.Serie, c.Correlativo, c.Estado })
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return pedidos.Select(p => new PedidoJornadaDto
            {
                PedidoId = p.PedidoId,
                FechaOperativa = p.JornadaId is int jornadaId && fechasJornada.TryGetValue(jornadaId, out var fecha) ? fecha : null,
                MesaNumero = p.MesaNumero,
                SalonNombre = p.SalonNombre,
                Mozo = p.Mozo,
                CreadoEn = p.CreadoEn,
                Estado = p.EstadoPedido,
                Total = p.Total,
                Pagado = p.Pagos.Sum(g => g.Monto),
                // En un pedido anulado todos los platos quedan "Cancelado": se muestra cuántos tenía.
                CantidadPlatos = p.EstadoPedido == EstadoPedido.Cancelado ? p.PlatosAnulados : p.Platos,
                Comentario = p.Comentario,
                MetodosPago = p.Pagos.Select(g => g.Metodo).Distinct().ToList(),
                Comprobante = p.Comprobante == null ? null : new ComprobanteResumenDto
                {
                    ComprobanteId = p.Comprobante.ComprobanteId,
                    Tipo = p.Comprobante.TipoComprobante,
                    Numero = $"{p.Comprobante.Serie}-{p.Comprobante.Correlativo:D8}",
                    Estado = p.Comprobante.Estado
                }
            }).ToList();
    }
}
