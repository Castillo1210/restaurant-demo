using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Comprobantes.DTOs;
using CaraNegra.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Comprobantes.Commands;

/// <summary>
/// Botón "Reintentar" en caja: vuelve a enviar (o consulta) un comprobante PorEnviar,
/// Pendiente o con ErrorEnvio, con el MISMO número.
/// </summary>
public record ReenviarComprobanteCommand(int ComprobanteId) : IRequest<ComprobanteDto>;

public class ReenviarComprobanteCommandHandler : IRequestHandler<ReenviarComprobanteCommand, ComprobanteDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IFacturacionElectronicaService _facturacion;
    private readonly IDatosEmpresaProvider _empresa;

    public ReenviarComprobanteCommandHandler(IApplicationDbContext context, IFacturacionElectronicaService facturacion,
        IDatosEmpresaProvider empresa)
    {
        _context = context;
        _facturacion = facturacion;
        _empresa = empresa;
    }

    public async Task<ComprobanteDto> Handle(ReenviarComprobanteCommand request, CancellationToken cancellationToken)
    {
        if (_facturacion.EsSimulado)
            throw new InvalidOperationException("La facturación está en modo Simulado: no hay envío a SUNAT.");

        var c = await ComprobanteQueries.ConDetalle(_context)
            .FirstOrDefaultAsync(x => x.ComprobanteId == request.ComprobanteId, cancellationToken)
            ?? throw new KeyNotFoundException($"Comprobante {request.ComprobanteId} no encontrado");

        if (!ComprobanteEnvio.SePuedeReintentar(c.Estado))
            throw new InvalidOperationException($"El comprobante está {c.Estado}: no hay nada que reenviar.");

        // Nunca mandar a producción algo emitido en pruebas (o al revés).
        if (!string.Equals(c.Ambiente, _facturacion.Ambiente, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Este comprobante se emitió en {c.Ambiente} y el sistema ahora está en {_facturacion.Ambiente}: no se puede reenviar.");

        // Si tras el error ya se emitió OTRO comprobante para el pedido, este no se reenvía
        // (quedarían dos comprobantes válidos por la misma venta).
        if (c.Estado == EstadoComprobante.ErrorEnvio)
        {
            var hayOtro = await _context.Comprobantes.AnyAsync(x =>
                x.PedidoId == c.PedidoId && x.ComprobanteId != c.ComprobanteId
                && x.Estado != EstadoComprobante.Anulado
                && x.Estado != EstadoComprobante.Rechazado
                && x.Estado != EstadoComprobante.ErrorEnvio, cancellationToken);
            if (hayOtro)
                throw new InvalidOperationException("Este pedido ya tiene otro comprobante emitido; este no se reenvía.");
        }

        await ComprobanteEnvio.ProcesarAsync(c, _facturacion, cancellationToken,
            async ct => await _context.SaveChangesAsync(ct));
        await _context.SaveChangesAsync(cancellationToken);

        return ComprobanteMapper.ToDto(c, await _empresa.ObtenerAsync(cancellationToken));
    }
}

/// <summary>
/// Lo ejecuta cada pocos minutos un proceso en segundo plano: reenvía los PorEnviar y consulta
/// los Pendiente del ambiente actual. Devuelve cuántos procesó.
/// </summary>
public record ProcesarComprobantesPendientesCommand(int Maximo = 20) : IRequest<int>;

public class ProcesarComprobantesPendientesCommandHandler : IRequestHandler<ProcesarComprobantesPendientesCommand, int>
{
    // Primeros intentos: cada vuelta del proceso (cada pocos minutos). Después se sigue insistiendo,
    // pero más espaciado, hasta 7 días desde la emisión (un corte largo de internet no debe dejar
    // boletas sin enviar). Mientras tanto, caja ve el aviso de comprobantes atrasados.
    private const int IntentosRapidos = 50;
    private static readonly TimeSpan EsperaLenta = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan PlazoMaximo = TimeSpan.FromDays(7);

    private readonly IApplicationDbContext _context;
    private readonly IFacturacionElectronicaService _facturacion;

    public ProcesarComprobantesPendientesCommandHandler(IApplicationDbContext context, IFacturacionElectronicaService facturacion)
    {
        _context = context;
        _facturacion = facturacion;
    }

    public async Task<int> Handle(ProcesarComprobantesPendientesCommand request, CancellationToken cancellationToken)
    {
        if (_facturacion.EsSimulado) return 0;

        var ambiente = _facturacion.Ambiente;
        // No tocar lo que se está emitiendo en este momento desde caja.
        var limite = DateTime.UtcNow.AddSeconds(-60);
        var limiteLento = DateTime.UtcNow - EsperaLenta;
        var desde = DateTime.UtcNow - PlazoMaximo;

        // Comparaciones explícitas (no array.Contains) para que EF lo traduzca bien en .NET 10.
        var ids = await _context.Comprobantes
            .Where(c => (c.Estado == EstadoComprobante.PorEnviar || c.Estado == EstadoComprobante.Pendiente)
                && c.Ambiente == ambiente
                && c.FechaEmision > desde
                && (c.UltimoEnvioEn == null
                    || (c.IntentosEnvio < IntentosRapidos && c.UltimoEnvioEn < limite)
                    || c.UltimoEnvioEn < limiteLento))
            .OrderBy(c => c.FechaEmision)
            .Select(c => c.ComprobanteId)
            .Take(Math.Clamp(request.Maximo, 1, 100))
            .ToListAsync(cancellationToken);

        var procesados = 0;
        foreach (var id in ids)
        {
            var c = await ComprobanteQueries.ConDetalle(_context)
                .FirstOrDefaultAsync(x => x.ComprobanteId == id, cancellationToken);
            if (c is null) continue;

            await ComprobanteEnvio.ProcesarAsync(c, _facturacion, cancellationToken,
                async ct => await _context.SaveChangesAsync(ct));
            await _context.SaveChangesAsync(cancellationToken);
            procesados++;
        }

        return procesados;
    }
}
