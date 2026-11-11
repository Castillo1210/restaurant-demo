using CaraNegra.Application.Common;
using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Pagos.DTOs;
using CaraNegra.Application.Pedidos.DTOs;
using CaraNegra.Domain.Entities;
using CaraNegra.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Pedidos.Commands;

/// <summary>
/// Quita (anula) un producto de un pedido ya enviado a cocina: todo el ítem o solo algunas
/// unidades. Si es el último plato del pedido, se anula el pedido completo (AnulacionPedido). SIEMPRE queda registrado en ProductosAnulados quién lo hizo, cuándo, cuánto, en qué
/// estado estaba en cocina y el motivo (obligatorio), y se avisa a la ticketera de cocina.
/// </summary>
/// <param name="SoloPedidosPropios">true para el rol MOZO: solo puede quitar de las mesas que él atiende.</param>
/// <param name="ImprimeEnCaja">Quita el administrador o el cajero: el aviso "ANULADO" lo imprime su propio equipo, no cocina.</param>
public record EliminarDetallePedidoCommand(int PedidoId, int DetallePedidoId, int UsuarioId, string? Motivo, int? Cantidad = null,
    bool SoloPedidosPropios = false, bool ImprimeEnCaja = false) : IRequest<PedidoDto>;

public class EliminarDetallePedidoCommandHandler : IRequestHandler<EliminarDetallePedidoCommand, PedidoDto>
{
    public const int MotivoMinimo = 3;
    public const int MotivoMaximo = 200;

    private readonly IApplicationDbContext _context;
    private readonly IPedidosHubService _hub;
    private readonly IImpresoraCocinaService _impresora;

    public EliminarDetallePedidoCommandHandler(IApplicationDbContext context, IPedidosHubService hub, IImpresoraCocinaService impresora)
    {
        _context = context;
        _hub = hub;
        _impresora = impresora;
    }

    public async Task<PedidoDto> Handle(EliminarDetallePedidoCommand request, CancellationToken cancellationToken)
    {
        var motivo = (request.Motivo ?? string.Empty).Trim();
        if (motivo.Length < MotivoMinimo)
            throw new InvalidOperationException("Indica el motivo para quitar el producto (queda registrado).");
        if (motivo.Length > MotivoMaximo) motivo = motivo[..MotivoMaximo];

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

        // Pendiente o En preparación: cualquiera. Listo (el plato ya salió de cocina, p. ej. el cliente
        // lo rechazó): solo el cajero o el admin.
        var estadoPermitido = pedido.EstadoPedido is EstadoPedido.Pendiente or EstadoPedido.EnPreparacion
            || (pedido.EstadoPedido == EstadoPedido.Listo && !request.SoloPedidosPropios);
        if (!estadoPermitido)
        {
            throw new InvalidOperationException(pedido.EstadoPedido == EstadoPedido.Listo
                ? "El pedido ya está listo: solo el cajero o el administrador pueden quitar platos."
                : "Este pedido ya fue cobrado o anulado: no se pueden quitar platos.");
        }

        // Con pagos registrados, quitar un plato dejaría el pedido pagado de más y ya no se podría
        // cobrar ni cerrar (igual que cambiar un precio o aplicar un descuento).
        if (pedido.Pagos.Any(p => !p.EstaAnulado))
        {
            throw new InvalidOperationException(
                "Este pedido ya tiene pagos registrados: anula el pago en Caja antes de quitar platos.");
        }

        // El mozo solo puede quitar productos de SUS mesas (las de otro mozo: el cajero o el admin).
        if (request.SoloPedidosPropios && pedido.UsuarioId != request.UsuarioId)
        {
            throw new InvalidOperationException(
                $"Esta mesa la atiende {pedido.Usuario?.NombreCompleto ?? "otro mozo"}: solo él (o el cajero / admin) puede quitar productos.");
        }

        var detalle = pedido.DetallesPedidos.FirstOrDefault(d => d.DetallePedidoId == request.DetallePedidoId)
            ?? throw new KeyNotFoundException($"El detalle {request.DetallePedidoId} no pertenece al pedido {request.PedidoId}");

        var cantidad = request.Cantidad ?? detalle.Cantidad;
        if (cantidad < 1 || cantidad > detalle.Cantidad)
            throw new InvalidOperationException($"La cantidad a quitar debe estar entre 1 y {detalle.Cantidad}");
        var quitaTodo = cantidad == detalle.Cantidad;

        var usuario = await _context.Usuarios.AsNoTracking()
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.UsuarioId == request.UsuarioId, cancellationToken)
            ?? throw new KeyNotFoundException("Usuario no encontrado");

        // Quitar el ÚLTIMO plato = anular el pedido completo: mismo motivo y registro, aviso
        // "PEDIDO ANULADO" a cocina y la mesa queda libre. Así no hace falta otro botón.
        if (quitaTodo && pedido.DetallesPedidos.Count(d => d.EstadoDetallePedido != EstadoDetallePedido.Cancelado) <= 1)
        {
            var resultado = await AnulacionPedido.AplicarAsync(_context, pedido, usuario, motivo, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await tx.ConfirmarAsync(cancellationToken);
            await AnulacionPedido.NotificarAsync(_hub, _impresora, pedido, usuario.NombreCompleto, motivo, resultado, cancellationToken,
                paraCaja: request.ImprimeEnCaja);
            return MapToDto(pedido);
        }

        // Auditoría: queda la marca de quién quitó qué, cuándo y por qué.
        var mesaTexto = AnulacionPedido.EtiquetaMesa(pedido.Mesa?.Salon?.Nombre, pedido.Mesa?.NumeroMesa);
        var productoNombre = detalle.Producto?.Nombre ?? "Producto";
        _context.ProductosAnulados.Add(new ProductoAnulado
        {
            PedidoId = pedido.PedidoId,
            JornadaId = pedido.JornadaId,
            ProductoId = detalle.ProductoId,
            ProductoNombre = productoNombre,
            Cantidad = cantidad,
            PrecioUnitario = detalle.Monto,
            Importe = detalle.Monto * cantidad,
            EstadoItem = detalle.EstadoDetallePedido.ToString(),
            Motivo = motivo,
            Mesa = mesaTexto,
            UsuarioId = usuario.UsuarioId,
            UsuarioNombre = usuario.NombreCompleto,
            UsuarioRol = usuario.Rol?.Nombre ?? string.Empty,
            AnuladoEn = DateTime.UtcNow
        });

        pedido.SubTotal -= detalle.Monto * cantidad;
        if (pedido.SubTotal < 0) pedido.SubTotal = 0;
        var descuentoActivo = pedido.DetallesDescuentos.FirstOrDefault()?.Descuento;
        pedido.Total = DescuentoCalculator.CalcularTotal(pedido.SubTotal, descuentoActivo);

        if (quitaTodo)
        {
            _context.DetallesPedido.Remove(detalle);
            pedido.DetallesPedidos.Remove(detalle);
        }
        else
        {
            detalle.Cantidad -= cantidad;
        }

        // Si el ítem quitado era el único que cocina tenía pendiente, el pedido puede quedar listo.
        var estadoPedidoAnterior = pedido.EstadoPedido;
        pedido.EstadoPedido = CaraNegra.Application.Cocina.CocinaEstadoHelper.CalcularEstadoPedido(pedido);

        await _context.SaveChangesAsync(cancellationToken);
        await tx.ConfirmarAsync(cancellationToken);

        if (estadoPedidoAnterior != pedido.EstadoPedido)
        {
            await _hub.NotificarPedidoEstadoCambiado(new PedidoEstadoCambiadoEvent
            {
                PedidoId = pedido.PedidoId,
                MesaNumero = pedido.Mesa?.NumeroMesa ?? string.Empty,
                SalonNombre = pedido.Mesa?.Salon?.Nombre ?? string.Empty,
                EstadoAnterior = estadoPedidoAnterior.ToString(),
                EstadoNuevo = pedido.EstadoPedido.ToString(),
                ActualizadoEn = DateTime.UtcNow
            });
        }

        await _hub.NotificarPedidoActualizado(new PedidoActualizadoEvent
        {
            PedidoId = pedido.PedidoId,
            MesaNumero = pedido.Mesa.NumeroMesa,
            SubTotal = pedido.SubTotal,
            Total = pedido.Total
        });

        // Aviso a la ticketera de cocina para que no lo preparen (nunca lanza excepción).
        await _impresora.ImprimirComandaAsync(new ComandaCocina
        {
            PedidoId = pedido.PedidoId,
            MesaNumero = pedido.Mesa?.NumeroMesa ?? string.Empty,
            SalonNombre = pedido.Mesa?.Salon?.Nombre ?? string.Empty,
            MozoNombre = usuario.NombreCompleto,
            CreadoEn = DateTime.UtcNow,
            EsAnulacion = true,
            Motivo = motivo,
            ParaCaja = request.ImprimeEnCaja,
            Items = new List<ItemComanda> { new() { ProductoNombre = productoNombre, Cantidad = cantidad } }
        }, cancellationToken);

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
