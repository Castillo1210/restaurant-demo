using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Pagos.DTOs;
using CaraNegra.Application.Pedidos.DTOs;
using CaraNegra.Domain.Entities;
using CaraNegra.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Pedidos.Commands;

public record UpdatePedidoEstadoCommand(int PedidoId, UpdatePedidoEstadoDto Dto) : IRequest<PedidoDto>;

public class UpdatePedidoEstadoCommandHandler : IRequestHandler<UpdatePedidoEstadoCommand, PedidoDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IPedidosHubService _hub;

    // Transiciones manuales permitidas (mozo/cajero/admin) vía este endpoint.
    // "Entregado" es un estado terminal que solo se alcanza automáticamente cuando
    // CreatePagoCommand detecta que el pedido quedó completamente pagado: no se
    // permite forzarlo manualmente aquí para no dejar pedidos "entregados" sin cobrar.
    private static readonly Dictionary<EstadoPedido, EstadoPedido[]> TransicionesPermitidas = new()
    {
        [EstadoPedido.Pendiente] = new[] { EstadoPedido.EnPreparacion, EstadoPedido.Cancelado },
        [EstadoPedido.EnPreparacion] = new[] { EstadoPedido.Listo, EstadoPedido.Cancelado },
        [EstadoPedido.Listo] = new[] { EstadoPedido.Cancelado },
        [EstadoPedido.Entregado] = Array.Empty<EstadoPedido>(),
        [EstadoPedido.Cancelado] = Array.Empty<EstadoPedido>()
    };

    public UpdatePedidoEstadoCommandHandler(IApplicationDbContext context, IPedidosHubService hub)
    {
        _context = context;
        _hub = hub;
    }

    public async Task<PedidoDto> Handle(UpdatePedidoEstadoCommand request, CancellationToken cancellationToken)
    {
        // Bloquea el pedido: si llega otra operación sobre él a la vez, espera a que esta termine.
        await using var tx = await _context.IniciarTransaccionAsync(cancellationToken);
        await _context.BloquearPedidoAsync(request.PedidoId, cancellationToken);

        var pedido = await _context.Pedidos
            .Include(p => p.Mesa).ThenInclude(m => m.Salon)
            .Include(p => p.Usuario)
            .Include(p => p.DetallesPedidos)
                .ThenInclude(d => d.Producto)
            .Include(p => p.Pagos)
                .ThenInclude(p => p.MetodoPago)
            .Include(p => p.DetallesDescuentos)
                .ThenInclude(dd => dd.Descuento)
            .FirstOrDefaultAsync(p => p.PedidoId == request.PedidoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Pedido {request.PedidoId} no encontrado");

        var estadoAnterior = pedido.EstadoPedido;
        var estadoNuevo = request.Dto.EstadoPedido;

        // Anular un pedido exige motivo y deja registro (quién, qué platos, por qué) y avisa a
        // cocina: eso lo hace AnularPedidoCommand (POST pedidos/{id}/anular), no este cambio de estado.
        if (estadoNuevo == EstadoPedido.Cancelado)
        {
            throw new InvalidOperationException("Para anular un pedido usa «Anular pedido» (pide el motivo y avisa a cocina).");
        }

        if (estadoAnterior == estadoNuevo)
        {
            throw new InvalidOperationException($"El pedido ya se encuentra en estado {estadoAnterior}");
        }

        if (!TransicionesPermitidas.TryGetValue(estadoAnterior, out var permitidas) || !permitidas.Contains(estadoNuevo))
        {
            throw new InvalidOperationException($"No se puede cambiar el pedido de {estadoAnterior} a {estadoNuevo}");
        }

        // Mantener la pantalla de cocina coherente cuando el estado se cambia a mano (mozo/caja):
        // los ítems activos acompañan al pedido.
        if (estadoNuevo is EstadoPedido.EnPreparacion or EstadoPedido.Listo)
        {
            foreach (var detalle in pedido.DetallesPedidos)
            {
                if (estadoNuevo == EstadoPedido.EnPreparacion && detalle.EstadoDetallePedido == EstadoDetallePedido.Pendiente)
                    detalle.EstadoDetallePedido = EstadoDetallePedido.EnPreparacion;
                else if (estadoNuevo == EstadoPedido.Listo
                    && detalle.EstadoDetallePedido is EstadoDetallePedido.Pendiente or EstadoDetallePedido.EnPreparacion)
                    detalle.EstadoDetallePedido = EstadoDetallePedido.Listo;
            }
        }

        pedido.EstadoPedido = estadoNuevo;

        await _context.SaveChangesAsync(cancellationToken);
        await tx.ConfirmarAsync(cancellationToken);

        await _hub.NotificarPedidoEstadoCambiado(new PedidoEstadoCambiadoEvent
        {
            PedidoId = pedido.PedidoId,
            MesaNumero = pedido.Mesa.NumeroMesa,
            SalonNombre = pedido.Mesa?.Salon?.Nombre ?? string.Empty,
            EstadoAnterior = estadoAnterior.ToString(),
            EstadoNuevo = estadoNuevo.ToString(),
            ActualizadoEn = DateTime.UtcNow
        });

        return MapToDto(pedido);
    }

    private static DescuentoAplicadoDto? MapDescuento(Pedido pedido)
    {
        var detalle = pedido.DetallesDescuentos.FirstOrDefault();
        if (detalle?.Descuento == null) return null;
        return new DescuentoAplicadoDto
        {
            DescuentoId = detalle.Descuentoid,
            Nombre = detalle.Descuento.Nombre,
            EsPorcentaje = detalle.Descuento.EsPorcentaje,
            Valor = detalle.Descuento.Valor,
            MontoDescuento = pedido.SubTotal - pedido.Total
        };
    }

    private PedidoDto MapToDto(Pedido pedido)
    {
        return new PedidoDto
        {
            PedidoId = pedido.PedidoId,
            MesaId = pedido.MesaId,
            SalonNombre = pedido.Mesa?.Salon?.Nombre ?? string.Empty,
            MesaNumero = pedido.Mesa?.NumeroMesa ?? string.Empty,
            UsuarioId = pedido.UsuarioId,
            UsuarioNombre = pedido.Usuario?.NombreCompleto ?? string.Empty,
            SubTotal = pedido.SubTotal,
            Total = pedido.Total,
            EstadoPedido = pedido.EstadoPedido,
            Comentario = pedido.Comentario,
            CreadoEn = pedido.CreadoEn,
            Descuento = MapDescuento(pedido),
            Detalles = pedido.DetallesPedidos.Select(d => new PedidoDetalleDto
            {
                DetallePedidoId = d.DetallePedidoId,
                ProductoId = d.ProductoId,
                ProductoNombre = d.Producto?.Nombre ?? string.Empty,
                Cantidad = d.Cantidad,
                Monto = d.Monto,
                Notas = d.Notas,
                EstadoDetallePedido = d.EstadoDetallePedido
            }).ToList(),
            Pagos = pedido.Pagos.Select(p => new PagoDto
            {
                PagoId = p.PagoId,
                PedidoId = p.PedidoId,
                MesaNumero = pedido.Mesa?.NumeroMesa ?? string.Empty,
                Monto = p.Monto,
                MetodoPagoId = p.MetodoPagoId,
                MetodoPagoNombre = p.MetodoPago?.Nombre ?? string.Empty,
                Referencia = p.Referencia,
                EstaAnulado = p.EstaAnulado,
                MotivoAnulacion = p.MotivoAnulacion,
                AnuladoEn = p.AnuladoEn,
                AnuladoPorUsuarioId = p.AnuladoPorUsuarioId,
                CreadoEn = p.CreadoEn
            }).ToList()
        };
    }
}
