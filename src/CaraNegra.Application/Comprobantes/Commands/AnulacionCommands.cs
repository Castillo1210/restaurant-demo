using CaraNegra.Application.Common;
using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Comprobantes.DTOs;
using CaraNegra.Domain.Entities;
using CaraNegra.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Comprobantes.Commands;

// ─────────────────────────────── Anular ───────────────────────────────

/// <summary>
/// Anula una boleta o factura aceptada. El sistema elige el camino según el tipo y los días
/// (ver ComprobanteAnulacion): comunicación de baja, resumen diario o nota de crédito 01.
/// <paramref name="UsarNotaCredito"/> fuerza la nota de crédito (p. ej. si la baja fue rechazada).
/// La anulación solo deja sin efecto el comprobante: el pago y el pedido no se tocan (después se
/// puede emitir otro comprobante o anular el pago en caja).
/// </summary>
public record AnularComprobanteCommand(int ComprobanteId, string? Motivo, bool UsarNotaCredito, int UsuarioId)
    : IRequest<ComprobanteDto>;

public class AnularComprobanteCommandHandler : IRequestHandler<AnularComprobanteCommand, ComprobanteDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IFacturacionElectronicaService _facturacion;
    private readonly IDatosEmpresaProvider _empresa;

    public AnularComprobanteCommandHandler(IApplicationDbContext context, IFacturacionElectronicaService facturacion,
        IDatosEmpresaProvider empresa)
    {
        _context = context;
        _facturacion = facturacion;
        _empresa = empresa;
    }

    public async Task<ComprobanteDto> Handle(AnularComprobanteCommand request, CancellationToken cancellationToken)
    {
        var motivo = (request.Motivo ?? string.Empty).Trim();
        if (motivo.Length < ComprobanteAnulacion.MotivoMinimo)
            throw new InvalidOperationException("Indica el motivo de la anulación (queda registrado y se envía a SUNAT).");
        if (motivo.Length > ComprobanteAnulacion.MotivoMaximo) motivo = motivo[..ComprobanteAnulacion.MotivoMaximo];

        var pedidoId = await _context.Comprobantes.AsNoTracking()
            .Where(c => c.ComprobanteId == request.ComprobanteId)
            .Select(c => (int?)c.PedidoId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Comprobante {request.ComprobanteId} no encontrado");

        // Mismo bloqueo que emitir / anular pago: dos anulaciones a la vez no pasan.
        await using var tx = await _context.IniciarTransaccionAsync(cancellationToken);
        await _context.BloquearPedidoAsync(pedidoId, cancellationToken);

        var c = await ComprobanteQueries.ConDetalle(_context)
            .FirstAsync(x => x.ComprobanteId == request.ComprobanteId, cancellationToken);

        var noSePuede = ComprobanteAnulacion.MotivoNoSePuede(c);
        if (noSePuede != null) throw new InvalidOperationException(noSePuede);

        if (!_facturacion.EsSimulado && !string.Equals(c.Ambiente, _facturacion.Ambiente, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Este comprobante se emitió en {c.Ambiente} y el sistema ahora está en {_facturacion.Ambiente}: no se puede anular desde aquí.");

        var metodo = request.UsarNotaCredito ? ComprobanteAnulacion.ConNotaCredito : ComprobanteAnulacion.MetodoPara(c);

        c.AnulacionMetodo = metodo;
        c.AnulacionMotivo = motivo;
        c.AnulacionSolicitadaEn = DateTime.UtcNow;
        c.AnuladoPorUsuarioId = request.UsuarioId;
        c.AnulacionIntentos = 0;
        c.AnulacionUltimoEnvioEn = null;
        c.AnulacionMensaje = null;

        NotaCredito? nota = null;
        if (metodo == ComprobanteAnulacion.ConNotaCredito)
        {
            nota = await NotasCreditoAlta.CrearAsync(_context, _facturacion, c, ComprobanteAnulacion.CodigoAnulacionOperacion,
                motivo, null, request.UsuarioId, cancellationToken);
            ComprobanteAnulacion.SincronizarConNota(c, nota);
        }
        else if (_facturacion.EsSimulado)
        {
            c.Estado = EstadoComprobante.Anulado;
            c.AnulacionEstado = EstadoAnulacion.Aceptada;
            c.AnulacionMensaje = $"Anulado (simulado) por {ComprobanteAnulacion.DescripcionMetodo(metodo)}.";
        }
        else
        {
            c.AnulacionEstado = EstadoAnulacion.PorEnviar;
            // Marca de "en envío": el proceso automático no la toca durante el primer minuto.
            c.AnulacionUltimoEnvioEn = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
        await tx.ConfirmarAsync(cancellationToken);

        // Envío al proveedor, ya fuera del bloqueo. Sin conexión queda PorEnviar y se reintenta solo.
        if (!_facturacion.EsSimulado)
        {
            if (nota != null)
            {
                await NotaCreditoEnvio.ProcesarAsync(nota, c, _facturacion, cancellationToken,
                    async ct => await _context.SaveChangesAsync(ct));
                ComprobanteAnulacion.SincronizarConNota(c, nota);
            }
            else
            {
                await AnulacionEnvio.ProcesarAsync(c, _facturacion, cancellationToken,
                    async ct => await _context.SaveChangesAsync(ct));
            }
            await _context.SaveChangesAsync(cancellationToken);
        }

        return ComprobanteMapper.ToDto(c, await _empresa.ObtenerAsync(cancellationToken));
    }
}

// ─────────────────────── Reintentar / consultar ───────────────────────

/// <summary>
/// Botón "Consultar / reintentar" de una anulación en curso (PorEnviar o Pendiente): reenvía la
/// baja / resumen / nota de crédito o consulta su estado.
/// </summary>
public record ReintentarAnulacionCommand(int ComprobanteId) : IRequest<ComprobanteDto>;

public class ReintentarAnulacionCommandHandler : IRequestHandler<ReintentarAnulacionCommand, ComprobanteDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IFacturacionElectronicaService _facturacion;
    private readonly IDatosEmpresaProvider _empresa;

    public ReintentarAnulacionCommandHandler(IApplicationDbContext context, IFacturacionElectronicaService facturacion,
        IDatosEmpresaProvider empresa)
    {
        _context = context;
        _facturacion = facturacion;
        _empresa = empresa;
    }

    public async Task<ComprobanteDto> Handle(ReintentarAnulacionCommand request, CancellationToken cancellationToken)
    {
        if (_facturacion.EsSimulado)
            throw new InvalidOperationException("La facturación está en modo Simulado: no hay envío a SUNAT.");

        var c = await ComprobanteQueries.ConDetalle(_context)
            .FirstOrDefaultAsync(x => x.ComprobanteId == request.ComprobanteId, cancellationToken)
            ?? throw new KeyNotFoundException($"Comprobante {request.ComprobanteId} no encontrado");

        if (!ComprobanteAnulacion.EnCurso(c))
            throw new InvalidOperationException("Este comprobante no tiene una anulación en proceso.");

        await AnulacionPendiente.ProcesarAsync(_context, _facturacion, c, cancellationToken);

        return ComprobanteMapper.ToDto(c, await _empresa.ObtenerAsync(cancellationToken));
    }
}

/// <summary>Procesa una anulación en curso (la usan el botón de reintento y el proceso automático).</summary>
public static class AnulacionPendiente
{
    public static async Task ProcesarAsync(IApplicationDbContext context, IFacturacionElectronicaService facturacion,
        Comprobante c, CancellationToken ct)
    {
        if (c.AnulacionMetodo == ComprobanteAnulacion.ConNotaCredito)
        {
            var nota = c.NotasCredito
                .Where(n => n.Codigo == ComprobanteAnulacion.CodigoAnulacionOperacion)
                .OrderByDescending(n => n.NotaCreditoId)
                .FirstOrDefault();
            if (nota == null) return;

            if (nota.Estado is EstadoComprobante.PorEnviar or EstadoComprobante.Pendiente)
                await NotaCreditoEnvio.ProcesarAsync(nota, c, facturacion, ct, async t => await context.SaveChangesAsync(t));
            ComprobanteAnulacion.SincronizarConNota(c, nota);
        }
        else
        {
            await AnulacionEnvio.ProcesarAsync(c, facturacion, ct, async t => await context.SaveChangesAsync(t));
        }
        await context.SaveChangesAsync(ct);
    }
}

// ───────────────────── Confirmar a mano (respaldo) ─────────────────────

/// <summary>
/// Respaldo para una baja / resumen que se quedó "Pendiente": APISUNAT no documenta cómo consultar
/// su estado. Si el administrador verificó en el panel de APISUNAT que SUNAT la aceptó, la marca
/// como aceptada.
/// </summary>
public record ConfirmarAnulacionCommand(int ComprobanteId, int UsuarioId) : IRequest<ComprobanteDto>;

public class ConfirmarAnulacionCommandHandler : IRequestHandler<ConfirmarAnulacionCommand, ComprobanteDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IDatosEmpresaProvider _empresa;

    public ConfirmarAnulacionCommandHandler(IApplicationDbContext context, IDatosEmpresaProvider empresa)
    {
        _context = context;
        _empresa = empresa;
    }

    public async Task<ComprobanteDto> Handle(ConfirmarAnulacionCommand request, CancellationToken cancellationToken)
    {
        var c = await ComprobanteQueries.ConDetalle(_context)
            .FirstOrDefaultAsync(x => x.ComprobanteId == request.ComprobanteId, cancellationToken)
            ?? throw new KeyNotFoundException($"Comprobante {request.ComprobanteId} no encontrado");

        if (c.AnulacionEstado != EstadoAnulacion.Pendiente
            || c.AnulacionMetodo is not (ComprobanteAnulacion.Baja or ComprobanteAnulacion.Resumen))
            throw new InvalidOperationException("Solo se confirma a mano una comunicación de baja o un resumen diario pendiente.");

        var usuario = await _context.Usuarios.AsNoTracking()
            .Where(u => u.UsuarioId == request.UsuarioId)
            .Select(u => u.NombreCompleto)
            .FirstOrDefaultAsync(cancellationToken) ?? "el administrador";

        c.Estado = EstadoComprobante.Anulado;
        c.AnulacionEstado = EstadoAnulacion.Aceptada;
        c.AnulacionMensaje = $"Confirmada a mano por {usuario} (verificado en el panel de APISUNAT) el " +
            $"{PeruDateRangeHelper.ToHoraPeru(DateTime.UtcNow):dd/MM/yyyy HH:mm}.";
        await _context.SaveChangesAsync(cancellationToken);

        return ComprobanteMapper.ToDto(c, await _empresa.ObtenerAsync(cancellationToken));
    }
}

// ─────────────── Nota de crédito por corrección de descripción ───────────────

/// <summary>
/// Nota de crédito 03 "Corrección por error en la descripción": importes en cero; solo corrige
/// textos. El comprobante sigue vigente.
/// </summary>
public record EmitirNotaCorreccionCommand(int ComprobanteId, string? Motivo, List<string>? Correcciones, int UsuarioId)
    : IRequest<ComprobanteDto>;

public class EmitirNotaCorreccionCommandHandler : IRequestHandler<EmitirNotaCorreccionCommand, ComprobanteDto>
{
    private const int MaxLineas = 20;
    private const int MaxLargoLinea = 250;

    private readonly IApplicationDbContext _context;
    private readonly IFacturacionElectronicaService _facturacion;
    private readonly IDatosEmpresaProvider _empresa;

    public EmitirNotaCorreccionCommandHandler(IApplicationDbContext context, IFacturacionElectronicaService facturacion,
        IDatosEmpresaProvider empresa)
    {
        _context = context;
        _facturacion = facturacion;
        _empresa = empresa;
    }

    public async Task<ComprobanteDto> Handle(EmitirNotaCorreccionCommand request, CancellationToken cancellationToken)
    {
        var lineas = (request.Correcciones ?? new List<string>())
            .Select(l => (l ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim())
            .Where(l => l.Length > 0)
            .ToList();
        if (lineas.Count == 0)
            throw new InvalidOperationException("Escribe al menos una descripción corregida.");
        if (lineas.Count > MaxLineas)
            throw new InvalidOperationException($"Como máximo {MaxLineas} líneas por nota de crédito.");
        if (lineas.Any(l => l.Length < 3))
            throw new InvalidOperationException("Cada corrección debe tener al menos 3 caracteres.");
        lineas = lineas
            .Select(l => l.StartsWith("Corrección", StringComparison.OrdinalIgnoreCase) ? l : $"Corrección: {l}")
            .Select(l => l.Length > MaxLargoLinea ? l[..MaxLargoLinea] : l)
            .ToList();

        var motivo = string.IsNullOrWhiteSpace(request.Motivo)
            ? ComprobanteAnulacion.DescripcionCodigo(ComprobanteAnulacion.CodigoCorreccionDescripcion)
            : request.Motivo.Trim();
        if (motivo.Length > ComprobanteAnulacion.MotivoMaximo) motivo = motivo[..ComprobanteAnulacion.MotivoMaximo];

        var pedidoId = await _context.Comprobantes.AsNoTracking()
            .Where(c => c.ComprobanteId == request.ComprobanteId)
            .Select(c => (int?)c.PedidoId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Comprobante {request.ComprobanteId} no encontrado");

        await using var tx = await _context.IniciarTransaccionAsync(cancellationToken);
        await _context.BloquearPedidoAsync(pedidoId, cancellationToken);

        var c = await ComprobanteQueries.ConDetalle(_context)
            .FirstAsync(x => x.ComprobanteId == request.ComprobanteId, cancellationToken);

        if (!ComprobanteAnulacion.EstaVigente(c.Estado) || ComprobanteAnulacion.EnCurso(c))
            throw new InvalidOperationException("Solo se corrige un comprobante aceptado y sin anulación en curso.");
        if (!_facturacion.EsSimulado && !string.Equals(c.Ambiente, _facturacion.Ambiente, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Este comprobante se emitió en {c.Ambiente} y el sistema ahora está en {_facturacion.Ambiente}.");

        var nota = await NotasCreditoAlta.CrearAsync(_context, _facturacion, c, ComprobanteAnulacion.CodigoCorreccionDescripcion,
            motivo, string.Join("\n", lineas), request.UsuarioId, cancellationToken);
        await tx.ConfirmarAsync(cancellationToken);

        if (!_facturacion.EsSimulado)
        {
            await NotaCreditoEnvio.ProcesarAsync(nota, c, _facturacion, cancellationToken,
                async ct => await _context.SaveChangesAsync(ct));
            await _context.SaveChangesAsync(cancellationToken);
        }

        return ComprobanteMapper.ToDto(c, await _empresa.ObtenerAsync(cancellationToken));
    }
}

/// <summary>Reenvía / consulta una nota de crédito PorEnviar, Pendiente o con error de envío.</summary>
public record ReenviarNotaCreditoCommand(int NotaCreditoId) : IRequest<ComprobanteDto>;

public class ReenviarNotaCreditoCommandHandler : IRequestHandler<ReenviarNotaCreditoCommand, ComprobanteDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IFacturacionElectronicaService _facturacion;
    private readonly IDatosEmpresaProvider _empresa;

    public ReenviarNotaCreditoCommandHandler(IApplicationDbContext context, IFacturacionElectronicaService facturacion,
        IDatosEmpresaProvider empresa)
    {
        _context = context;
        _facturacion = facturacion;
        _empresa = empresa;
    }

    public async Task<ComprobanteDto> Handle(ReenviarNotaCreditoCommand request, CancellationToken cancellationToken)
    {
        if (_facturacion.EsSimulado)
            throw new InvalidOperationException("La facturación está en modo Simulado: no hay envío a SUNAT.");

        var comprobanteId = await _context.NotasCredito.AsNoTracking()
            .Where(n => n.NotaCreditoId == request.NotaCreditoId)
            .Select(n => (int?)n.ComprobanteId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Nota de crédito {request.NotaCreditoId} no encontrada");

        var c = await ComprobanteQueries.ConDetalle(_context)
            .FirstAsync(x => x.ComprobanteId == comprobanteId, cancellationToken);
        var nota = c.NotasCredito.First(n => n.NotaCreditoId == request.NotaCreditoId);

        if (!ComprobanteEnvio.SePuedeReintentar(nota.Estado))
            throw new InvalidOperationException($"La nota de crédito está {nota.Estado}: no hay nada que reenviar.");
        if (!string.Equals(nota.Ambiente, _facturacion.Ambiente, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Esta nota se emitió en {nota.Ambiente} y el sistema ahora está en {_facturacion.Ambiente}: no se puede reenviar.");

        // Error de datos: se vuelve a intentar como envío nuevo (mismo número).
        if (nota.Estado == EstadoComprobante.ErrorEnvio)
        {
            nota.Estado = EstadoComprobante.PorEnviar;
            nota.IntentosEnvio = 0;
        }

        await NotaCreditoEnvio.ProcesarAsync(nota, c, _facturacion, cancellationToken,
            async ct => await _context.SaveChangesAsync(ct));
        ComprobanteAnulacion.SincronizarConNota(c, nota);
        await _context.SaveChangesAsync(cancellationToken);

        return ComprobanteMapper.ToDto(c, await _empresa.ObtenerAsync(cancellationToken));
    }
}

/// <summary>Alta de una nota de crédito con su número (dentro de la transacción del que llama).</summary>
public static class NotasCreditoAlta
{
    public static async Task<NotaCredito> CrearAsync(IApplicationDbContext context, IFacturacionElectronicaService facturacion,
        Comprobante c, string codigo, string motivo, string? correcciones, int usuarioId, CancellationToken ct)
    {
        var esAnulacion = codigo == ComprobanteAnulacion.CodigoAnulacionOperacion;
        var serie = facturacion.SerieNotaCredito(c.TipoComprobante);

        var nota = new NotaCredito
        {
            ComprobanteId = c.ComprobanteId,
            Serie = serie,
            Codigo = codigo,
            Motivo = motivo,
            Correcciones = correcciones,
            OpGravada = esAnulacion ? c.OpGravada : 0,
            Igv = esAnulacion ? c.Igv : 0,
            Total = esAnulacion ? c.Total : 0,
            FechaEmision = DateTime.UtcNow,
            Estado = facturacion.EsSimulado ? EstadoComprobante.AceptadoSimulado : EstadoComprobante.PorEnviar,
            Ambiente = facturacion.Ambiente,
            UsuarioId = usuarioId,
            UltimoEnvioEn = facturacion.EsSimulado ? null : DateTime.UtcNow
        };
        c.NotasCredito.Add(nota);

        // Correlativo por serie; el índice único (Serie, Correlativo) protege contra choques.
        const int maxIntentos = 3;
        for (var intento = 1; ; intento++)
        {
            nota.Correlativo = await ComprobanteAnulacion.UltimoNumeroNotaAsync(context, facturacion, serie, ct) + 1;
            nota.HashResumen = facturacion.EsSimulado
                ? ComprobanteAnulacion.HashSimulado($"{c.EmisorRuc}|07|{serie}|{nota.Correlativo}|{nota.Total:0.00}|{nota.FechaEmision:O}")
                : string.Empty;
            try
            {
                await context.SaveChangesAsync(ct);
                break;
            }
            catch (DbUpdateException) when (intento < maxIntentos)
            {
                // Otro tomó el número: se recalcula.
            }
        }

        // El envío lo hace el que llama, ya fuera del bloqueo; mientras tanto el proceso automático
        // no la toca (UltimoEnvioEn reciente).
        return nota;
    }
}
