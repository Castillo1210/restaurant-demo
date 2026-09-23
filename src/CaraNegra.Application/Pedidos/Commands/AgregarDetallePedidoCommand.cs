using CaraNegra.Application.Common;
using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Pagos.DTOs;
using CaraNegra.Application.Pedidos.DTOs;
using CaraNegra.Domain.Entities;
using CaraNegra.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Pedidos.Commands;

public record AgregarDetallePedidoCommand(int PedidoId, CreatePedidoDetalleDto Dto) : IRequest<PedidoDto>;

public class AgregarDetallePedidoCommandHandler : IRequestHandler<AgregarDetallePedidoCommand, PedidoDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IPedidosHubService _hub;
    private readonly IImpresoraCocinaService _impresora;

    public AgregarDetallePedidoCommandHandler(IApplicationDbContext context, IPedidosHubService hub, IImpresoraCocinaService impresora)
    {
        _context = context;
        _hub = hub;
        _impresora = impresora;
    }

    public async Task<PedidoDto> Handle(AgregarDetallePedidoCommand request, CancellationToken cancellationToken)
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

        // Con la pantalla de cocina un pedido pasa a "Listo" apenas cocina termina, así que
        // también se permite agregar ítems a un pedido Listo (p.ej. el cliente pide un postre
        // después): el pedido vuelve a "En preparación" para que cocina lo vea de nuevo.
        if (pedido.EstadoPedido != EstadoPedido.Pendiente
            && pedido.EstadoPedido != EstadoPedido.EnPreparacion
            && pedido.EstadoPedido != EstadoPedido.Listo)
        {
            throw new InvalidOperationException("Solo se pueden agregar items a pedidos Pendientes, En Preparación o Listos");
        }

        var estadoPedidoAnterior = pedido.EstadoPedido;
        if (pedido.EstadoPedido == EstadoPedido.Listo)
        {
            pedido.EstadoPedido = EstadoPedido.EnPreparacion;
        }

        if (request.Dto.ProductoId <= 0)
        {
            throw new InvalidOperationException("El producto es requerido");
        }

        var producto = await _context.Productos
            .FirstOrDefaultAsync(p => p.ProductoId == request.Dto.ProductoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Producto {request.Dto.ProductoId} no encontrado");

        if (!producto.EstaDisponible)
        {
            throw new InvalidOperationException($"El producto {producto.Nombre} no está disponible");
        }

        if (request.Dto.Cantidad <= 0)
        {
            throw new InvalidOperationException("La cantidad debe ser mayor a 0");
        }

        var detalle = new DetallePedido
        {
            PedidoId = pedido.PedidoId,
            ProductoId = producto.ProductoId,
            Cantidad = request.Dto.Cantidad,
            Monto = producto.Precio,
            Notas = request.Dto.Notas,
            EstadoDetallePedido = EstadoDetallePedido.Pendiente
        };

        _context.DetallesPedido.Add(detalle);

        pedido.SubTotal += detalle.Monto * detalle.Cantidad;
        var descuentoActivo = pedido.DetallesDescuentos.FirstOrDefault()?.Descuento;
        pedido.Total = DescuentoCalculator.CalcularTotal(pedido.SubTotal, descuentoActivo);

        await _context.SaveChangesAsync(cancellationToken);

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

        await _hub.NotificarPedidoActualizado(new PedidoActualizadoEvent
        {
            PedidoId = pedidoActualizado.PedidoId,
            MesaNumero = pedidoActualizado.Mesa.NumeroMesa,
            SubTotal = pedidoActualizado.SubTotal,
            Total = pedidoActualizado.Total
        });

        if (estadoPedidoAnterior != pedidoActualizado.EstadoPedido)
        {
            await _hub.NotificarPedidoEstadoCambiado(new PedidoEstadoCambiadoEvent
            {
                PedidoId = pedidoActualizado.PedidoId,
                MesaNumero = pedidoActualizado.Mesa?.NumeroMesa ?? string.Empty,
                EstadoAnterior = estadoPedidoAnterior.ToString(),
                EstadoNuevo = pedidoActualizado.EstadoPedido.ToString(),
                ActualizadoEn = DateTime.UtcNow
            });
        }

        // Comanda adicional de cocina (Fase 6): solo el ítem recién agregado, nunca lanza excepción.
        await _impresora.ImprimirComandaAsync(new ComandaCocina
        {
            PedidoId = pedidoActualizado.PedidoId,
            MesaNumero = pedidoActualizado.Mesa?.NumeroMesa ?? string.Empty,
            MozoNombre = pedidoActualizado.Usuario?.NombreCompleto ?? string.Empty,
            CreadoEn = detalle.CreadoEn,
            EsAdicional = true,
            Items = new List<ItemComanda>
            {
                new ItemComanda
                {
                    ProductoNombre = producto.Nombre,
                    Cantidad = detalle.Cantidad,
                    Notas = detalle.Notas
                }
            }
        }, cancellationToken);

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
