using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Pedidos.DTOs;
using CaraNegra.Application.Pagos.DTOs;
using CaraNegra.Domain.Entities;
using CaraNegra.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Pedidos.Commands;

public record UpdatePedidoCommand(int PedidoId, UpdatePedidoDto Dto) : IRequest<PedidoDto>;

public class UpdatePedidoCommandHandler : IRequestHandler<UpdatePedidoCommand, PedidoDto>
{
    private readonly IApplicationDbContext _context;

    public UpdatePedidoCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<PedidoDto> Handle(UpdatePedidoCommand request, CancellationToken cancellationToken)
    {
        var pedido = await _context.Pedidos
            .Include(p => p.Mesa)
            .Include(p => p.Usuario)
            .Include(p => p.DetallesPedidos)
                .ThenInclude(d => d.Producto)
            .Include(p => p.Pagos)
                .ThenInclude(p => p.MetodoPago)
            .Include(p => p.DetallesDescuentos)
                .ThenInclude(dd => dd.Descuento)
            .FirstOrDefaultAsync(p => p.PedidoId == request.PedidoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Pedido {request.PedidoId} no encontrado");

        // Solo permitir actualizar mesa y usuario si el pedido está en estado Pendiente
        if (pedido.EstadoPedido != EstadoPedido.Pendiente)
        {
            throw new InvalidOperationException("Solo se puede actualizar la mesa/usuario en pedidos Pendientes");
        }

        var mesa = await _context.Mesas
            .FirstOrDefaultAsync(m => m.MesaId == request.Dto.MesaId, cancellationToken)
            ?? throw new KeyNotFoundException($"Mesa {request.Dto.MesaId} no encontrada");

        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.UsuarioId == request.Dto.UsuarioId, cancellationToken)
            ?? throw new KeyNotFoundException($"Usuario {request.Dto.UsuarioId} no encontrado");

        // Si cambia la mesa, liberar la anterior y ocupar la nueva
        if (pedido.MesaId != request.Dto.MesaId)
        {
            var mesaAnterior = await _context.Mesas.FindAsync(new object[] { pedido.MesaId }, cancellationToken);
            if (mesaAnterior != null)
            {
                // Verificar si hay otros pedidos en la mesa anterior
                var otrosPedidos = await _context.Pedidos
                    .AnyAsync(p => p.MesaId == pedido.MesaId && p.PedidoId != request.PedidoId 
                        && p.EstadoPedido != EstadoPedido.Cancelado && p.EstadoPedido != EstadoPedido.Entregado, cancellationToken);
                
                if (!otrosPedidos)
                {
                    mesaAnterior.Estado = EstadoMesa.Disponible;
                }
            }

            if (mesa.Estado != EstadoMesa.Disponible)
            {
                throw new InvalidOperationException($"La mesa {mesa.NumeroMesa} no está disponible");
            }
            mesa.Estado = EstadoMesa.Ocupada;
        }

        pedido.MesaId = request.Dto.MesaId;
        pedido.UsuarioId = request.Dto.UsuarioId;

        await _context.SaveChangesAsync(cancellationToken);

        // Recargar para el DTO
        var pedidoActualizado = await _context.Pedidos
            .Include(p => p.Mesa)
            .Include(p => p.Usuario)
            .Include(p => p.DetallesPedidos)
                .ThenInclude(d => d.Producto)
            .Include(p => p.Pagos)
                .ThenInclude(p => p.MetodoPago)
            .Include(p => p.DetallesDescuentos)
                .ThenInclude(dd => dd.Descuento)
            .FirstAsync(p => p.PedidoId == request.PedidoId, cancellationToken);

        return MapToDto(pedidoActualizado);
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
            MesaNumero = pedido.Mesa?.NumeroMesa ?? string.Empty,
            UsuarioId = pedido.UsuarioId,
            UsuarioNombre = pedido.Usuario?.NombreCompleto ?? string.Empty,
            SubTotal = pedido.SubTotal,
            Total = pedido.Total,
            EstadoPedido = pedido.EstadoPedido,
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