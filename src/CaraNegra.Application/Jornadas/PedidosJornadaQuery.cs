using CaraNegra.Application.Common.Interfaces;
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

public class PedidosJornadaDto
{
    public JornadaDto? Jornada { get; set; }
    public List<PedidoJornadaDto> Pedidos { get; set; } = new();
}

public class PedidoJornadaDto
{
    public int PedidoId { get; set; }
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

        var pedidos = await _context.Pedidos
            .AsNoTracking()
            .Where(p => p.JornadaId == jornada.JornadaId)
            .OrderByDescending(p => p.CreadoEn)
            .Select(p => new
            {
                p.PedidoId,
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

        return new PedidosJornadaDto
        {
            Jornada = JornadaCalculos.ToDto(jornada),
            Pedidos = pedidos.Select(p => new PedidoJornadaDto
            {
                PedidoId = p.PedidoId,
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
            }).ToList()
        };
    }
}
