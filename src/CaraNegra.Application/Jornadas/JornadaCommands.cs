using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Domain.Entities;
using CaraNegra.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Jornadas;

// ───────────── Abrir ─────────────

public record AbrirJornadaCommand(AbrirJornadaDto Dto, int UsuarioId) : IRequest<JornadaDto>;

public class AbrirJornadaCommandHandler : IRequestHandler<AbrirJornadaCommand, JornadaDto>
{
    private readonly IApplicationDbContext _context;

    public AbrirJornadaCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<JornadaDto> Handle(AbrirJornadaCommand request, CancellationToken cancellationToken)
    {
        // El admin y el cajero pueden abrir el día al mismo tiempo: se serializa para que no
        // queden dos jornadas abiertas.
        await using var tx = await _context.IniciarTransaccionAsync(cancellationToken);
        await _context.BloquearAperturaJornadaAsync(cancellationToken);

        var abierta = await JornadaReglas.ObtenerAbiertaAsync(_context, cancellationToken);
        if (abierta is not null)
            throw new InvalidOperationException(
                $"Ya hay una jornada abierta (del {abierta.FechaOperativa:dd/MM/yyyy}). Ciérrala antes de abrir otra.");

        var usuario = await _context.Usuarios.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UsuarioId == request.UsuarioId, cancellationToken)
            ?? throw new KeyNotFoundException("Usuario no encontrado");

        var jornada = new Jornada
        {
            FechaOperativa = (request.Dto.FechaOperativa ?? JornadaReglas.HoyLima()).Date,
            Estado = EstadoJornada.Abierta,
            AbiertaEn = DateTime.UtcNow,
            AbiertaPorUsuarioId = usuario.UsuarioId,
            AbiertaPor = usuario.NombreCompleto,
            FondoInicial = Math.Round(request.Dto.FondoInicial, 2),
            ObservacionApertura = string.IsNullOrWhiteSpace(request.Dto.Observacion) ? null : request.Dto.Observacion.Trim()
        };

        _context.Jornadas.Add(jornada);
        await _context.SaveChangesAsync(cancellationToken);
        await tx.ConfirmarAsync(cancellationToken);
        return JornadaCalculos.ToDto(jornada);
    }
}

// ───────────── Movimientos de caja ─────────────

public record RegistrarMovimientoCommand(RegistrarMovimientoDto Dto, int UsuarioId) : IRequest<MovimientoCajaDto>;

public class RegistrarMovimientoCommandHandler : IRequestHandler<RegistrarMovimientoCommand, MovimientoCajaDto>
{
    private readonly IApplicationDbContext _context;

    public RegistrarMovimientoCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<MovimientoCajaDto> Handle(RegistrarMovimientoCommand request, CancellationToken cancellationToken)
    {
        await using var tx = await _context.IniciarTransaccionAsync(cancellationToken);
        await _context.BloquearJornadaAbiertaAsync(exclusivo: false, cancellationToken);
        var jornada = await JornadaReglas.RequerirAbiertaAsync(_context, cancellationToken);
        var usuario = await _context.Usuarios.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UsuarioId == request.UsuarioId, cancellationToken)
            ?? throw new KeyNotFoundException("Usuario no encontrado");

        var movimiento = new MovimientoCaja
        {
            JornadaId = jornada.JornadaId,
            Tipo = request.Dto.Tipo,
            Monto = Math.Round(request.Dto.Monto, 2),
            Motivo = request.Dto.Motivo.Trim(),
            UsuarioId = usuario.UsuarioId,
            RegistradoPor = usuario.NombreCompleto
        };

        _context.MovimientosCaja.Add(movimiento);
        await _context.SaveChangesAsync(cancellationToken);
        await tx.ConfirmarAsync(cancellationToken);
        return JornadaCalculos.ToDto(movimiento);
    }
}

public record AnularMovimientoCommand(int MovimientoCajaId, int UsuarioId) : IRequest<MovimientoCajaDto>;

public class AnularMovimientoCommandHandler : IRequestHandler<AnularMovimientoCommand, MovimientoCajaDto>
{
    private readonly IApplicationDbContext _context;

    public AnularMovimientoCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<MovimientoCajaDto> Handle(AnularMovimientoCommand request, CancellationToken cancellationToken)
    {
        await using var tx = await _context.IniciarTransaccionAsync(cancellationToken);
        await _context.BloquearJornadaAbiertaAsync(exclusivo: false, cancellationToken);
        var movimiento = await _context.MovimientosCaja
            .Include(m => m.Jornada)
            .FirstOrDefaultAsync(m => m.MovimientoCajaId == request.MovimientoCajaId, cancellationToken)
            ?? throw new KeyNotFoundException("Movimiento no encontrado");

        if (movimiento.Jornada.Estado == EstadoJornada.Cerrada)
            throw new InvalidOperationException("La jornada ya está cerrada: sus movimientos no se pueden modificar");
        if (movimiento.EstaAnulado)
            throw new InvalidOperationException("El movimiento ya estaba anulado");

        var usuario = await _context.Usuarios.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UsuarioId == request.UsuarioId, cancellationToken);

        movimiento.EstaAnulado = true;
        movimiento.AnuladoEn = DateTime.UtcNow;
        movimiento.AnuladoPor = usuario?.NombreCompleto;
        await _context.SaveChangesAsync(cancellationToken);
        await tx.ConfirmarAsync(cancellationToken);
        return JornadaCalculos.ToDto(movimiento);
    }
}

// ───────────── Cerrar (con lo registrado, sin arqueo) ─────────────

public record CerrarJornadaCommand(CerrarJornadaDto Dto, int UsuarioId) : IRequest<ResumenJornadaDto>;

public class CerrarJornadaCommandHandler : IRequestHandler<CerrarJornadaCommand, ResumenJornadaDto>
{
    private readonly IApplicationDbContext _context;

    public CerrarJornadaCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<ResumenJornadaDto> Handle(CerrarJornadaCommand request, CancellationToken cancellationToken)
    {
        // Bloqueo EXCLUSIVO de la jornada: espera a que terminen los cobros / pedidos en curso
        // (que la bloquean en modo compartido) y, mientras se cierra, no entra ninguno nuevo.
        // Así el cierre refleja exactamente lo que quedó guardado.
        await using var tx = await _context.IniciarTransaccionAsync(cancellationToken);
        await _context.BloquearJornadaAbiertaAsync(exclusivo: true, cancellationToken);

        var jornada = await JornadaReglas.ObtenerAbiertaAsync(_context, cancellationToken)
            ?? throw new InvalidOperationException("No hay una jornada abierta (quizá otro usuario acaba de cerrarla). Actualiza la pantalla.");

        var abiertos = await PedidosAbiertos.ListarAsync(_context, cancellationToken);
        if (abiertos.Count > 0)
        {
            var mesas = string.Join(", ", abiertos.Take(6).Select(p => string.IsNullOrEmpty(p.SalonNombre)
                ? $"mesa {p.MesaNumero}" : $"{p.SalonNombre} {p.MesaNumero}"));
            throw new InvalidOperationException(
                $"No se puede cerrar: hay {abiertos.Count} pedido(s) sin cobrar ({mesas}{(abiertos.Count > 6 ? "…" : "")}). " +
                "Cóbralos o anúlalos primero.");
        }

        var usuario = await _context.Usuarios.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UsuarioId == request.UsuarioId, cancellationToken)
            ?? throw new KeyNotFoundException("Usuario no encontrado");

        var esperado = await JornadaCalculos.CalcularEsperadoAsync(_context, jornada, cancellationToken);

        // Sin arqueo: la jornada se cierra con lo REGISTRADO en el sistema. Se guarda "contado" igual
        // a lo esperado (diferencia 0) para que el reporte y el historial sigan teniendo el detalle
        // por medio de pago. Los cierres antiguos conservan su arqueo real.
        static decimal Contado(ArqueoDto a) => a.Esperado;

        foreach (var a in esperado)
        {
            var contado = Contado(a);
            _context.JornadaArqueos.Add(new JornadaArqueo
            {
                JornadaId = jornada.JornadaId,
                MetodoPagoId = a.MetodoPagoId,
                MetodoPago = a.MetodoPago,
                EsEfectivo = a.EsEfectivo,
                Ventas = a.Ventas,
                Esperado = a.Esperado,
                Contado = contado,
                Diferencia = contado - a.Esperado
            });
        }

        var movimientos = await _context.MovimientosCaja
            .AsNoTracking()
            .Where(m => m.JornadaId == jornada.JornadaId && !m.EstaAnulado)
            .ToListAsync(cancellationToken);

        var cantidadPedidos = await _context.Pedidos
            .CountAsync(p => p.JornadaId == jornada.JornadaId && p.EstadoPedido == EstadoPedido.Entregado, cancellationToken);

        var efectivo = esperado.Where(a => a.EsEfectivo).ToList();

        jornada.Estado = EstadoJornada.Cerrada;
        jornada.CerradaEn = DateTime.UtcNow;
        jornada.CerradaPorUsuarioId = usuario.UsuarioId;
        jornada.CerradaPor = usuario.NombreCompleto;
        jornada.ObservacionCierre = string.IsNullOrWhiteSpace(request.Dto.Observacion) ? null : request.Dto.Observacion.Trim();
        jornada.TotalVentas = esperado.Sum(a => a.Ventas);
        jornada.CantidadPedidos = cantidadPedidos;
        jornada.TotalIngresos = movimientos.Where(m => m.Tipo == TipoMovimientoCaja.Ingreso).Sum(m => m.Monto);
        jornada.TotalEgresos = movimientos.Where(m => m.Tipo == TipoMovimientoCaja.Egreso).Sum(m => m.Monto);
        jornada.EfectivoEsperado = efectivo.Sum(a => a.Esperado);
        jornada.EfectivoContado = efectivo.Sum(Contado);
        jornada.DiferenciaTotal = esperado.Sum(a => Contado(a) - a.Esperado);
        jornada.ActualizadoEn = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        await tx.ConfirmarAsync(cancellationToken);

        return await JornadaCalculos.ConstruirResumenAsync(_context, jornada, incluirMontos: true, cancellationToken);
    }
}
