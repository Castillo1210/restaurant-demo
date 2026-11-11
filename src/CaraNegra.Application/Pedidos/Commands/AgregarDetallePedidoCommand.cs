using CaraNegra.Application.Jornadas;
using CaraNegra.Application.Common;
using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Pagos.DTOs;
using CaraNegra.Application.Pedidos.DTOs;
using CaraNegra.Application.Pedidos;
using CaraNegra.Domain.Entities;
using CaraNegra.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Pedidos.Commands;

/// <summary>
/// Agrega uno o varios ítems a un pedido abierto. El carrito del mozo se envía completo en una
/// sola petición: todo o nada (si se corta el Wi-Fi no quedan platos a medias) y una sola
/// comanda adicional para cocina.
/// </summary>
/// <param name="Comentario">Comentario general del pedido junto con el envío: null = no se toca; "" lo borra.</param>
public record AgregarDetallePedidoCommand(int PedidoId, IReadOnlyList<CreatePedidoDetalleDto> Detalles,
    int? UsuarioActualId = null, bool PuedeCambiarPrecio = false, string? Comentario = null,
    string? OperacionId = null, bool ComandaEnCaja = false) : IRequest<PedidoDto>
{
    /// <summary>Un solo ítem (endpoint POST {id}/detalles, se mantiene por compatibilidad).</summary>
    public AgregarDetallePedidoCommand(int pedidoId, CreatePedidoDetalleDto dto) : this(pedidoId, new[] { dto }) { }
}

public class AgregarDetallePedidoCommandHandler : IRequestHandler<AgregarDetallePedidoCommand, PedidoDto>
{
    public const int MaximoItemsPorEnvio = 50;

    private readonly IApplicationDbContext _context;
    private readonly IPedidosHubService _hub;
    private readonly IImpresoraCocinaService _impresora;
    private readonly OperacionesRecientes _operaciones;

    public AgregarDetallePedidoCommandHandler(IApplicationDbContext context, IPedidosHubService hub, IImpresoraCocinaService impresora,
        OperacionesRecientes operaciones)
    {
        _context = context;
        _hub = hub;
        _impresora = impresora;
        _operaciones = operaciones;
    }

    public async Task<PedidoDto> Handle(AgregarDetallePedidoCommand request, CancellationToken cancellationToken)
    {
        var items = request.Detalles ?? Array.Empty<CreatePedidoDetalleDto>();
        if (items.Count == 0)
            throw new InvalidOperationException("No hay ítems para agregar");
        if (items.Count > MaximoItemsPorEnvio)
            throw new InvalidOperationException($"No se pueden agregar más de {MaximoItemsPorEnvio} ítems a la vez");
        foreach (var item in items)
        {
            if (item.ProductoId <= 0)
                throw new InvalidOperationException("El producto es requerido");
            if (item.Cantidad <= 0 || item.Cantidad > 100)
                throw new InvalidOperationException("La cantidad debe estar entre 1 y 100");
        }

        // Bloquea el pedido: dos mozos (o la caja y cocina) trabajando sobre la misma mesa a la vez
        // ya no se pisan el subtotal; la segunda operación espera y suma sobre lo ya guardado.
        await using var tx = await _context.IniciarTransaccionAsync(cancellationToken);
        await _context.BloquearJornadaAbiertaAsync(exclusivo: false, cancellationToken);
        await _context.BloquearPedidoAsync(request.PedidoId, cancellationToken);

        await JornadaReglas.RequerirAbiertaAsync(_context, cancellationToken);

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

        // Reintento de un envío que ya se grabó (se perdió la respuesta por el Wi-Fi): se devuelve
        // el pedido como quedó, sin volver a agregar los platos ni imprimir otro ticket.
        var claveOperacion = OperacionesRecientes.Clave(request.OperacionId);
        if (claveOperacion != null && _operaciones.YaProcesada(claveOperacion, out var pedidoPrevioId))
        {
            if (pedidoPrevioId == pedido.PedidoId)
                return MapToDto(pedido);

            // El mismo carrito ya quedó en OTRO pedido (p. ej. el mozo lo llevó a otra mesa tras
            // perder la respuesta): no se agrega aquí también.
            var otro = await _context.Pedidos.AsNoTracking()
                .Where(p => p.PedidoId == pedidoPrevioId)
                .Select(p => new { p.Mesa.NumeroMesa, Salon = p.Mesa.Salon != null ? p.Mesa.Salon.Nombre : null })
                .FirstOrDefaultAsync(cancellationToken);
            throw new InvalidOperationException(OperacionesRecientes.MensajeYaRegistrado(pedidoPrevioId,
                AnulacionPedido.EtiquetaMesa(otro?.Salon, otro?.NumeroMesa)));
        }

        // Con la pantalla de cocina un pedido pasa a "Listo" apenas cocina termina, así que
        // también se permite agregar ítems a un pedido Listo (p.ej. el cliente pide un postre
        // después): el pedido vuelve a "En preparación" para que cocina lo vea de nuevo.
        if (pedido.EstadoPedido != EstadoPedido.Pendiente
            && pedido.EstadoPedido != EstadoPedido.EnPreparacion
            && pedido.EstadoPedido != EstadoPedido.Listo)
        {
            throw new InvalidOperationException(
                "Este pedido ya no acepta más ítems (fue cobrado o cancelado). Actualiza la pantalla de mesas.");
        }

        // El mozo puede ajustar el comentario del pedido en el mismo envío: sale en esta comanda
        // adicional (sin un ticket aparte de "nota del pedido").
        if (request.Comentario != null)
            pedido.Comentario = ComentarioPedido.Normalizar(request.Comentario);

        var estadoPedidoAnterior = pedido.EstadoPedido;
        if (pedido.EstadoPedido == EstadoPedido.Listo)
        {
            pedido.EstadoPedido = EstadoPedido.EnPreparacion;
        }

        // Una consulta por producto distinto (sin Contains: ver notas de EF en .NET 10).
        var productos = new Dictionary<int, Producto>();
        foreach (var productoId in items.Select(i => i.ProductoId).Distinct())
        {
            var producto = await _context.Productos
                .FirstOrDefaultAsync(p => p.ProductoId == productoId, cancellationToken)
                ?? throw new KeyNotFoundException($"Producto {productoId} no encontrado");
            if (!producto.EstaDisponible)
                throw new InvalidOperationException($"El producto {producto.Nombre} no está disponible");
            productos[productoId] = producto;
        }

        var nuevos = new List<DetallePedido>();
        var cambiosPrecio = new List<(DetallePedido Detalle, string Producto, decimal PrecioCarta)>();
        foreach (var item in items)
        {
            var producto = productos[item.ProductoId];
            var detalle = new DetallePedido
            {
                PedidoId = pedido.PedidoId,
                ProductoId = producto.ProductoId,
                Cantidad = item.Cantidad,
                Monto = producto.Precio,
                Notas = NotaPlato.Normalizar(item.Notas),
                EstadoDetallePedido = EstadoDetallePedido.Pendiente
            };
            // Precio distinto al de la carta: solo cajero / admin, y queda registrado.
            if (item.PrecioUnitario is decimal precioPedido)
            {
                if (!request.PuedeCambiarPrecio) throw new InvalidOperationException(PreciosPedido.SinPermiso);
                var precio = PreciosPedido.Validar(precioPedido);
                if (precio != producto.Precio)
                {
                    detalle.Monto = precio;
                    cambiosPrecio.Add((detalle, producto.Nombre, producto.Precio));
                }
            }

            _context.DetallesPedido.Add(detalle);
            nuevos.Add(detalle);
            pedido.SubTotal += detalle.Monto * detalle.Cantidad;
        }

        var descuentoActivo = pedido.DetallesDescuentos.FirstOrDefault()?.Descuento;
        pedido.Total = DescuentoCalculator.CalcularTotal(pedido.SubTotal, descuentoActivo);

        await _context.SaveChangesAsync(cancellationToken);

        if (cambiosPrecio.Count > 0)
        {
            if (request.UsuarioActualId is not int quienId) throw new InvalidOperationException(PreciosPedido.SinPermiso);
            var quien = await PreciosPedido.UsuarioAsync(_context, quienId, cancellationToken);
            foreach (var c in cambiosPrecio)
                _context.CambiosPrecioPedido.Add(PreciosPedido.Registro(pedido, pedido.Mesa, c.Detalle, c.Producto, c.PrecioCarta, c.Detalle.Monto, "Precio al agregar al pedido", quien));
            await _context.SaveChangesAsync(cancellationToken);
        }

        // Se registra antes de confirmar: un reintento que espera el bloqueo del pedido ya lo encuentra.
        if (claveOperacion != null) _operaciones.Registrar(claveOperacion, pedido.PedidoId);
        try
        {
            await tx.ConfirmarAsync(cancellationToken);
        }
        catch
        {
            if (claveOperacion != null) _operaciones.Olvidar(claveOperacion);
            throw;
        }

        var pedidoActualizado = await _context.Pedidos
            .Include(p => p.Mesa).ThenInclude(m => m.Salon)
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
                SalonNombre = pedidoActualizado.Mesa?.Salon?.Nombre ?? string.Empty,
                EstadoAnterior = estadoPedidoAnterior.ToString(),
                EstadoNuevo = pedidoActualizado.EstadoPedido.ToString(),
                ActualizadoEn = DateTime.UtcNow
            });
        }

        // Comanda adicional de cocina (Fase 6): UNA sola con todo lo recién agregado (antes salía
        // una por plato). Fuera de la transacción: la impresora nunca retiene el bloqueo.
        await _impresora.ImprimirComandaAsync(new ComandaCocina
        {
            PedidoId = pedidoActualizado.PedidoId,
            MesaNumero = pedidoActualizado.Mesa?.NumeroMesa ?? string.Empty,
            SalonNombre = pedidoActualizado.Mesa?.Salon?.Nombre ?? string.Empty,
            MozoNombre = pedidoActualizado.Usuario?.NombreCompleto ?? string.Empty,
            CreadoEn = nuevos[0].CreadoEn,
            EsAdicional = true,
            ParaCaja = request.ComandaEnCaja,
            Comentario = pedidoActualizado.Comentario,
            Items = nuevos.Select(d => new ItemComanda
            {
                ProductoNombre = productos[d.ProductoId].Nombre,
                Cantidad = d.Cantidad,
                Notas = d.Notas
            }).ToList()
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
