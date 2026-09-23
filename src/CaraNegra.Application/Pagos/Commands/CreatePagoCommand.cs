using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Pagos.DTOs;
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

    public CreatePagoCommandHandler(IApplicationDbContext context, IPedidosHubService hub)
    {
        _context = context;
        _hub = hub;
    }

    public async Task<PagoDto> Handle(CreatePagoCommand request, CancellationToken cancellationToken)
    {
        // Obtener pedido con navegaciones necesarias
        var pedido = await _context.Pedidos
            .Include(p => p.Mesa)
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
            Referencia = request.Dto.Referencia
        };

        _context.Pagos.Add(pago);
        await _context.SaveChangesAsync(cancellationToken);

        // Actualizar estado del pedido si se completó el pago
        var nuevoTotalPagado = totalPagado + request.Dto.Monto;
        var esPagoCompleto = nuevoTotalPagado >= pedido.Total - 0.01m;
        var mesaLiberada = false;

        if (esPagoCompleto)
        {
            pedido.EstadoPedido = EstadoPedido.Entregado;

            // Liberar mesa si no hay otros pedidos activos. Importante: el cambio de estado
            // de arriba todavía no se guardó en la base de datos (el SaveChangesAsync está
            // más abajo), así que la consulta de "otros pedidos activos" debe excluir
            // explícitamente ESTE pedido — si no, se ve a sí mismo con su estado viejo
            // (todavía no Entregado) y nunca libera la mesa, ni siquiera cuando es el único
            // pedido activo de esa mesa.
            mesaLiberada = await LiberarMesaSiCorresponde(pedido.MesaId, pedido.PedidoId, cancellationToken);
        }
        else if (pedido.EstadoPedido == EstadoPedido.Listo)
        {
            // Si estaba listo y se hace pago parcial, mantener en Listo
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Recargar con navegaciones para el DTO
        var pagoCreado = await _context.Pagos
            .Include(p => p.MetodoPago)
            .Include(p => p.Pedido)
                .ThenInclude(p => p.Mesa)
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

    private async Task<bool> LiberarMesaSiCorresponde(int mesaId, int pedidoIdActual, CancellationToken ct)
    {
        var mesa = await _context.Mesas.FindAsync(new object[] { mesaId }, ct);
        if (mesa != null && mesa.Estado != EstadoMesa.Disponible)
        {
            var otrosPedidos = await _context.Pedidos
                .AnyAsync(p => p.MesaId == mesaId
                    && p.PedidoId != pedidoIdActual
                    && p.EstadoPedido != EstadoPedido.Cancelado
                    && p.EstadoPedido != EstadoPedido.Entregado, ct);

            if (!otrosPedidos)
            {
                mesa.Estado = EstadoMesa.Disponible;
                return true;
            }
        }
        return false;
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