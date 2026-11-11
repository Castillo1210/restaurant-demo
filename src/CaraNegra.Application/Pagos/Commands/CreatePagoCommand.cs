using CaraNegra.Application.Jornadas;
using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Mesas;
using CaraNegra.Application.Pagos.DTOs;
using CaraNegra.Application.Pedidos;
using CaraNegra.Domain.Entities;
using CaraNegra.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Pagos.Commands;

public record CreatePagoCommand(CreatePagoDto Dto) : IRequest<PagoDto>;

public class CreatePagoCommandHandler : IRequestHandler<CreatePagoCommand, PagoDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IPedidosHubService _hub;
    private readonly OperacionesRecientes _operaciones;

    public CreatePagoCommandHandler(IApplicationDbContext context, IPedidosHubService hub, OperacionesRecientes operaciones)
    {
        _context = context;
        _hub = hub;
        _operaciones = operaciones;
    }

    public async Task<PagoDto> Handle(CreatePagoCommand request, CancellationToken cancellationToken)
    {
        // Doble toque en "Cobrar", dos cajas o un reintento por Wi-Fi lento: el pedido se bloquea
        // y la segunda petición espera; al continuar ve el pago de la primera (saldo ya cubierto)
        // y se rechaza en vez de cobrar dos veces. La jornada se bloquea en modo compartido para
        // que el cierre de caja no ocurra a mitad de un cobro.
        await using var tx = await _context.IniciarTransaccionAsync(cancellationToken);
        await _context.BloquearJornadaAbiertaAsync(exclusivo: false, cancellationToken);
        await _context.BloquearPedidoAsync(request.Dto.PedidoId, cancellationToken);

        // Reintento de un cobro que ya se grabó (la respuesta se perdió por el Wi-Fi o tardó):
        // se devuelve ese mismo pago en vez de cobrar otra vez.
        var claveOperacion = OperacionesRecientes.Clave(request.Dto.OperacionId);
        if (claveOperacion != null && _operaciones.YaProcesada(claveOperacion, out var pagoPrevioId))
        {
            var previo = await _context.Pagos
                .Include(p => p.MetodoPago)
                .Include(p => p.Pedido).ThenInclude(p => p.Mesa).ThenInclude(m => m.Salon)
                .FirstOrDefaultAsync(p => p.PagoId == pagoPrevioId && p.PedidoId == request.Dto.PedidoId, cancellationToken);
            if (previo != null) return MapToDto(previo);
        }

        // Sin jornada abierta no se cobra: cada pago queda en la jornada (para el arqueo).
        var jornada = await JornadaReglas.RequerirAbiertaAsync(_context, cancellationToken);

        // Obtener pedido con navegaciones necesarias
        var pedido = await _context.Pedidos
            .Include(p => p.Mesa).ThenInclude(m => m.Salon)
            .Include(p => p.Pagos)
            .FirstOrDefaultAsync(p => p.PedidoId == request.Dto.PedidoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Pedido {request.Dto.PedidoId} no encontrado");

        // Validar estado del pedido
        if (pedido.EstadoPedido == EstadoPedido.Cancelado)
            throw new InvalidOperationException("No se puede registrar pago en un pedido cancelado");

        if (pedido.EstadoPedido == EstadoPedido.Entregado)
            throw new InvalidOperationException("El pedido ya fue entregado y pagado completamente");

        // Calcular saldo pendiente (los pagos anulados no cuentan como pagados)
        var totalPagado = pedido.Pagos.Where(p => !p.EstaAnulado).Sum(p => p.Monto);
        var saldoPendiente = pedido.Total - totalPagado;

        if (request.Dto.Monto > saldoPendiente + 0.01m)
            throw new InvalidOperationException($"El monto ({request.Dto.Monto:C}) excede el saldo pendiente ({saldoPendiente:C})");

        // Verificar método de pago
        var metodoPago = await _context.MetodosPago
            .FirstOrDefaultAsync(m => m.MetodoPagoId == request.Dto.MetodoPagoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Método de pago {request.Dto.MetodoPagoId} no encontrado");

        // Crear el pago
        var pago = new Pago
        {
            PedidoId = pedido.PedidoId,
            Monto = request.Dto.Monto,
            MetodoPagoId = metodoPago.MetodoPagoId,
            Referencia = request.Dto.Referencia,
            JornadaId = jornada.JornadaId
        };

        // Pedido de antes de las jornadas: queda en la jornada en que se cobra.
        pedido.JornadaId ??= jornada.JornadaId;

        _context.Pagos.Add(pago);
        await _context.SaveChangesAsync(cancellationToken);

        // Actualizar estado del pedido si se completó el pago
        var nuevoTotalPagado = totalPagado + request.Dto.Monto;
        var esPagoCompleto = nuevoTotalPagado >= pedido.Total - 0.01m;
        var mesaLiberada = false;

        if (esPagoCompleto)
        {
            pedido.EstadoPedido = EstadoPedido.Entregado;

            // Liberar la mesa si no le queda otro pedido abierto (ver MesaOcupacion: excluye
            // este pedido, cuyo nuevo estado todavía no está guardado).
            mesaLiberada = await MesaOcupacion.LiberarSiQuedaLibreAsync(_context, pedido.MesaId, pedido.PedidoId, cancellationToken);
        }
        else if (pedido.EstadoPedido == EstadoPedido.Listo)
        {
            // Si estaba listo y se hace pago parcial, mantener en Listo
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Se registra antes de confirmar: un reintento que espera el bloqueo del pedido ya lo encuentra.
        if (claveOperacion != null) _operaciones.Registrar(claveOperacion, pago.PagoId);
        try
        {
            await tx.ConfirmarAsync(cancellationToken);
        }
        catch
        {
            if (claveOperacion != null) _operaciones.Olvidar(claveOperacion);
            throw;
        }

        // Recargar con navegaciones para el DTO
        var pagoCreado = await _context.Pagos
            .Include(p => p.MetodoPago)
            .Include(p => p.Pedido)
                .ThenInclude(p => p.Mesa).ThenInclude(m => m.Salon)
            .FirstAsync(p => p.PagoId == pago.PagoId, cancellationToken);

        await _hub.NotificarPagoRecibido(new PagoRecibidoEvent
        {
            PedidoId = pedido.PedidoId,
            MesaNumero = pagoCreado.Pedido?.Mesa?.NumeroMesa ?? string.Empty,
            Monto = pago.Monto,
            MetodoPago = pagoCreado.MetodoPago?.Nombre ?? string.Empty,
            EsPagoCompleto = esPagoCompleto,
            EstadoPedido = pedido.EstadoPedido.ToString()
        });

        if (mesaLiberada)
        {
            await _hub.NotificarMesaEstadoCambiado(new MesaEstadoCambiadoEvent
            {
                MesaId = pedido.MesaId,
                NumeroMesa = pagoCreado.Pedido?.Mesa?.NumeroMesa ?? string.Empty,
                EstadoAnterior = EstadoMesa.Ocupada.ToString(),
                EstadoNuevo = EstadoMesa.Disponible.ToString()
            });
        }

        return MapToDto(pagoCreado);
    }

    private PagoDto MapToDto(Pago pago)
    {
        return new PagoDto
        {
            PagoId = pago.PagoId,
            PedidoId = pago.PedidoId,
            MesaNumero = pago.Pedido?.Mesa?.NumeroMesa ?? string.Empty,
            Monto = pago.Monto,
            MetodoPagoId = pago.MetodoPagoId,
            MetodoPagoNombre = pago.MetodoPago?.Nombre ?? string.Empty,
            Referencia = pago.Referencia,
            EstaAnulado = pago.EstaAnulado,
            MotivoAnulacion = pago.MotivoAnulacion,
            AnuladoEn = pago.AnuladoEn,
            AnuladoPorUsuarioId = pago.AnuladoPorUsuarioId,
            CreadoEn = pago.CreadoEn
        };
    }
}