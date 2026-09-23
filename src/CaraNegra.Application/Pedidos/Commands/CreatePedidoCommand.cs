using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Pagos.DTOs;
using CaraNegra.Application.Pedidos.DTOs;
using CaraNegra.Domain.Entities;
using CaraNegra.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Pedidos.Commands;

public record CreatePedidoCommand(CreatePedidoDto Dto) : IRequest<PedidoDto>;

public class CreatePedidoCommandHandler : IRequestHandler<CreatePedidoCommand, PedidoDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IPedidosHubService _hub;
    private readonly IImpresoraCocinaService _impresora;

    public CreatePedidoCommandHandler(IApplicationDbContext context, IPedidosHubService hub, IImpresoraCocinaService impresora)
    {
        _context = context;
        _hub = hub;
        _impresora = impresora;
    }

    public async Task<PedidoDto> Handle(CreatePedidoCommand request, CancellationToken cancellationToken)
    {
        // Verificar que la mesa está disponible
        var mesa = await _context.Mesas
            .FirstOrDefaultAsync(m => m.MesaId == request.Dto.MesaId, cancellationToken)
            ?? throw new KeyNotFoundException($"Mesa {request.Dto.MesaId} no encontrada");

        // Una mesa Reservada también puede recibir un pedido nuevo (el cliente de la
        // reserva se sienta y el mozo toma la orden); solo Ocupada/Mantenimiento bloquean.
        if (mesa.Estado != EstadoMesa.Disponible && mesa.Estado != EstadoMesa.Reservada)
        {
            throw new InvalidOperationException($"La mesa {mesa.NumeroMesa} no está disponible (estado actual: {mesa.Estado})");
        }

        // Verificar usuario
        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.UsuarioId == request.Dto.UsuarioId, cancellationToken)
            ?? throw new KeyNotFoundException($"Usuario {request.Dto.UsuarioId} no encontrado");

        // Verificar productos y calcular totales
        decimal subTotal = 0;
        var detalles = new List<DetallePedido>();

        foreach (var detalleDto in request.Dto.Detalles)
        {
            var producto = await _context.Productos
                .FirstOrDefaultAsync(p => p.ProductoId == detalleDto.ProductoId, cancellationToken)
                ?? throw new KeyNotFoundException($"Producto {detalleDto.ProductoId} no encontrado");

            if (!producto.EstaDisponible)
            {
                throw new InvalidOperationException($"El producto {producto.Nombre} no está disponible");
            }

            var detalle = new DetallePedido
            {
                ProductoId = producto.ProductoId,
                Cantidad = detalleDto.Cantidad,
                Monto = producto.Precio,
                Notas = detalleDto.Notas,
                EstadoDetallePedido = EstadoDetallePedido.Pendiente
            };

            subTotal += detalle.Monto * detalle.Cantidad;
            detalles.Add(detalle);
        }

        var total = subTotal; // Sin descuentos por ahora

        // Crear pedido
        var pedido = new Pedido
        {
            MesaId = mesa.MesaId,
            UsuarioId = usuario.UsuarioId,
            SubTotal = subTotal,
            Total = total,
            EstadoPedido = EstadoPedido.Pendiente,
            DetallesPedidos = detalles
        };

        // Cambiar estado de mesa a Ocupada
        mesa.Estado = EstadoMesa.Ocupada;

        _context.Pedidos.Add(pedido);
        await _context.SaveChangesAsync(cancellationToken);

        // Recargar con navegaciones para el DTO
        var pedidoCreado = await _context.Pedidos
            .Include(p => p.Mesa)
            .Include(p => p.Usuario)
            .Include(p => p.DetallesPedidos)
                .ThenInclude(d => d.Producto)
            .FirstAsync(p => p.PedidoId == pedido.PedidoId, cancellationToken);

        await _hub.NotificarNuevoPedido(new NuevoPedidoEvent
        {
            PedidoId = pedidoCreado.PedidoId,
            MesaNumero = pedidoCreado.Mesa?.NumeroMesa ?? string.Empty,
            MozoNombre = pedidoCreado.Usuario?.NombreCompleto ?? string.Empty,
            CreadoEn = pedidoCreado.CreadoEn,
            Detalles = pedidoCreado.DetallesPedidos.Select(d => new PedidoDetalleEvent
            {
                ProductoId = d.ProductoId,
                ProductoNombre = d.Producto?.Nombre ?? string.Empty,
                Cantidad = d.Cantidad,
                Notas = d.Notas
            }).ToList()
        });

        // Comanda de cocina (Fase 6): nunca lanza excepción, ver IImpresoraCocinaService.
        await _impresora.ImprimirComandaAsync(new ComandaCocina
        {
            PedidoId = pedidoCreado.PedidoId,
            MesaNumero = pedidoCreado.Mesa?.NumeroMesa ?? string.Empty,
            MozoNombre = pedidoCreado.Usuario?.NombreCompleto ?? string.Empty,
            CreadoEn = pedidoCreado.CreadoEn,
            EsAdicional = false,
            Items = pedidoCreado.DetallesPedidos.Select(d => new ItemComanda
            {
                ProductoNombre = d.Producto?.Nombre ?? string.Empty,
                Cantidad = d.Cantidad,
                Notas = d.Notas
            }).ToList()
        }, cancellationToken);

        return MapToDto(pedidoCreado);
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