using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Jornadas;

// ───────────── Jornada actual (todos los roles) ─────────────

public record GetJornadaActualQuery : IRequest<JornadaActualDto>;

public class GetJornadaActualQueryHandler : IRequestHandler<GetJornadaActualQuery, JornadaActualDto>
{
    private readonly IApplicationDbContext _context;

    public GetJornadaActualQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<JornadaActualDto> Handle(GetJornadaActualQuery request, CancellationToken cancellationToken)
    {
        var jornada = await JornadaReglas.ObtenerAbiertaAsync(_context, cancellationToken);
        return new JornadaActualDto
        {
            Abierta = jornada is not null,
            Jornada = jornada is null ? null : JornadaCalculos.ToDto(jornada)
        };
    }
}

// ───────────── Resumen de la jornada abierta o de una cerrada ─────────────

/// <param name="JornadaId">Null = la jornada abierta.</param>
/// <param name="IncluirMontos">
/// False para el CAJERO mientras la jornada está abierta (arqueo ciego: no ve lo esperado antes
/// de contar). Las jornadas cerradas siempre muestran todo.
/// </param>
public record GetResumenJornadaQuery(int? JornadaId, bool IncluirMontos) : IRequest<ResumenJornadaDto>;

public class GetResumenJornadaQueryHandler : IRequestHandler<GetResumenJornadaQuery, ResumenJornadaDto>
{
    private readonly IApplicationDbContext _context;

    public GetResumenJornadaQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<ResumenJornadaDto> Handle(GetResumenJornadaQuery request, CancellationToken cancellationToken)
    {
        var jornada = request.JornadaId is int id
            ? await _context.Jornadas.AsNoTracking().FirstOrDefaultAsync(j => j.JornadaId == id, cancellationToken)
                ?? throw new KeyNotFoundException($"Jornada {id} no encontrada")
            : await JornadaReglas.ObtenerAbiertaAsync(_context, cancellationToken)
                ?? throw new KeyNotFoundException("No hay una jornada abierta");

        return await JornadaCalculos.ConstruirResumenAsync(_context, jornada, request.IncluirMontos, cancellationToken);
    }
}

// ───────────── Revisión previa al cierre ─────────────

public record GetPreCierreQuery : IRequest<PreCierreDto>;

public class GetPreCierreQueryHandler : IRequestHandler<GetPreCierreQuery, PreCierreDto>
{
    private readonly IApplicationDbContext _context;

    public GetPreCierreQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<PreCierreDto> Handle(GetPreCierreQuery request, CancellationToken cancellationToken)
    {
        var jornada = await JornadaReglas.ObtenerAbiertaAsync(_context, cancellationToken)
            ?? throw new KeyNotFoundException("No hay una jornada abierta");

        var esperado = await JornadaCalculos.CalcularEsperadoAsync(_context, jornada, cancellationToken);

        var comprobantesPendientes = await _context.Comprobantes
            .CountAsync(c => c.Pedido.JornadaId == jornada.JornadaId
                && (c.Estado == EstadoComprobante.PorEnviar || c.Estado == EstadoComprobante.Pendiente), cancellationToken);

        return new PreCierreDto
        {
            Jornada = JornadaCalculos.ToDto(jornada),
            PedidosAbiertos = await PedidosAbiertos.ListarAsync(_context, cancellationToken),
            ComprobantesPendientes = comprobantesPendientes,
            // Sin montos: el arqueo es ciego.
            Metodos = esperado.Select(a => new MetodoCierreDto
            {
                MetodoPagoId = a.MetodoPagoId,
                Nombre = a.MetodoPago,
                EsEfectivo = a.EsEfectivo
            }).ToList()
        };
    }
}

// ───────────── Historial (ADMIN) ─────────────

public record GetJornadasQuery(int Cantidad = 60) : IRequest<List<JornadaDto>>;

public class GetJornadasQueryHandler : IRequestHandler<GetJornadasQuery, List<JornadaDto>>
{
    private readonly IApplicationDbContext _context;

    public GetJornadasQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<JornadaDto>> Handle(GetJornadasQuery request, CancellationToken cancellationToken)
    {
        var jornadas = await _context.Jornadas
            .AsNoTracking()
            .OrderByDescending(j => j.JornadaId)
            .Take(Math.Clamp(request.Cantidad, 1, 365))
            .ToListAsync(cancellationToken);
        return jornadas.Select(JornadaCalculos.ToDto).ToList();
    }
}

/// <summary>Pedidos sin cobrar (Pendiente, En preparación o Listo): bloquean el cierre.</summary>
internal static class PedidosAbiertos
{
    public static async Task<List<PedidoAbiertoDto>> ListarAsync(IApplicationDbContext context, CancellationToken ct)
    {
        // Comparaciones explícitas (no array.Contains) para que EF lo traduzca bien en .NET 10.
        var pedidos = await context.Pedidos
            .AsNoTracking()
            .Include(p => p.Mesa).ThenInclude(m => m.Salon)
            .Include(p => p.Usuario)
            .Include(p => p.Pagos)
            .Where(p => p.EstadoPedido == EstadoPedido.Pendiente
                || p.EstadoPedido == EstadoPedido.EnPreparacion
                || p.EstadoPedido == EstadoPedido.Listo)
            .OrderBy(p => p.PedidoId)
            .ToListAsync(ct);

        return pedidos.Select(p => new PedidoAbiertoDto
        {
            PedidoId = p.PedidoId,
            MesaNumero = p.Mesa?.NumeroMesa ?? string.Empty,
            SalonNombre = p.Mesa?.Salon?.Nombre ?? string.Empty,
            Mozo = p.Usuario?.NombreCompleto ?? string.Empty,
            Estado = p.EstadoPedido.ToString(),
            Total = p.Total,
            Pagado = p.Pagos.Where(x => !x.EstaAnulado).Sum(x => x.Monto)
        }).ToList();
    }
}
