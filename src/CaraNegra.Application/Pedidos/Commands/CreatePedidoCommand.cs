using CaraNegra.Application.Jornadas;
using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Mesas;
using CaraNegra.Application.Pagos.DTOs;
using CaraNegra.Application.Pedidos.DTOs;
using CaraNegra.Application.Pedidos;
using CaraNegra.Domain.Entities;
using CaraNegra.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Pedidos.Commands;

/// <param name="UsuarioActualId">Quien envía el pedido (para registrar cambios de precio).</param>
/// <param name="PuedeCambiarPrecio">true para CAJERO / ADMIN: aceptan PrecioUnitario distinto a la carta.</param>
/// <param name="ComandaEnCaja">Pedido del administrador o del cajero: la comanda la imprime su equipo, no sale en cocina.</param>
public record CreatePedidoCommand(CreatePedidoDto Dto, int? UsuarioActualId = null, bool PuedeCambiarPrecio = false,
    bool ComandaEnCaja = false) : IRequest<PedidoDto>;

public class CreatePedidoCommandHandler : IRequestHandler<CreatePedidoCommand, PedidoDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IPedidosHubService _hub;
    private readonly IImpresoraCocinaService _impresora;
    private readonly OperacionesRecientes _operaciones;

    public CreatePedidoCommandHandler(IApplicationDbContext context, IPedidosHubService hub, IImpresoraCocinaService impresora,
        OperacionesRecientes operaciones)
    {
        _context = context;
        _hub = hub;
        _impresora = impresora;
        _operaciones = operaciones;
    }

    public async Task<PedidoDto> Handle(CreatePedidoCommand request, CancellationToken cancellationToken)
    {
        // Dos mozos abriendo la misma mesa libre a la vez (o un doble toque en "Enviar"): la mesa
        // se bloquea y la segunda petición espera; al continuar la ve Ocupada y se rechaza, en vez
        // de crear dos pedidos en la misma mesa.
        await using var tx = await _context.IniciarTransaccionAsync(cancellationToken);
        await _context.BloquearJornadaAbiertaAsync(exclusivo: false, cancellationToken);
        await _context.BloquearMesaAsync(request.Dto.MesaId, cancellationToken);

        // Reintento de un envío que ya creó el pedido (se perdió la respuesta por el Wi-Fi): se
        // devuelve ese pedido en vez de responder "la mesa ya tiene un pedido abierto".
        var claveOperacion = OperacionesRecientes.Clave(request.Dto.OperacionId);
        if (claveOperacion != null && _operaciones.YaProcesada(claveOperacion, out var pedidoPrevioId))
        {
            var previo = await _context.Pedidos
                .Include(p => p.Mesa).ThenInclude(m => m.Salon)
                .Include(p => p.Usuario)
                .Include(p => p.DetallesPedidos)
                    .ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(p => p.PedidoId == pedidoPrevioId, cancellationToken);
            if (previo != null)
            {
                if (previo.MesaId == request.Dto.MesaId) return MapToDto(previo);
                throw new InvalidOperationException(OperacionesRecientes.MensajeYaRegistrado(previo.PedidoId,
                    AnulacionPedido.EtiquetaMesa(previo.Mesa?.Salon?.Nombre, previo.Mesa?.NumeroMesa)));
            }
        }

        // Sin jornada abierta no se toman pedidos (el día se abre y se cierra con arqueo).
        var jornada = await JornadaReglas.RequerirAbiertaAsync(_context, cancellationToken);

        // Verificar que la mesa está disponible
        var mesa = await _context.Mesas
            .Include(m => m.Salon)
            .FirstOrDefaultAsync(m => m.MesaId == request.Dto.MesaId, cancellationToken)
            ?? throw new KeyNotFoundException($"Mesa {request.Dto.MesaId} no encontrada");

        if (mesa.Salon is { EstaActivo: false })
        {
            throw new InvalidOperationException($"El salón {mesa.Salon.Nombre} está desactivado; no se pueden tomar pedidos en sus mesas");
        }

        // Una mesa Reservada también puede recibir un pedido nuevo (el cliente de la
        // reserva se sienta y el mozo toma la orden); solo Ocupada/Mantenimiento bloquean.
        if (mesa.Estado == EstadoMesa.Ocupada)
        {
            throw new InvalidOperationException(
                $"La mesa {mesa.NumeroMesa} ya tiene un pedido abierto (quizá otro mozo la tomó hace un momento). " +
                "Vuelve a las mesas y agrega los platos a ese pedido.");
        }
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
        var cambiosPrecio = new List<(DetallePedido Detalle, string Producto, decimal PrecioCarta)>();

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
                Notas = NotaPlato.Normalizar(detalleDto.Notas),
                EstadoDetallePedido = EstadoDetallePedido.Pendiente
            };

            // Precio distinto al de la carta: solo cajero / admin, y queda registrado.
            if (detalleDto.PrecioUnitario is decimal precioPedido)
            {
                if (!request.PuedeCambiarPrecio) throw new InvalidOperationException(PreciosPedido.SinPermiso);
                var precio = PreciosPedido.Validar(precioPedido);
                if (precio != producto.Precio)
                {
                    detalle.Monto = precio;
                    cambiosPrecio.Add((detalle, producto.Nombre, producto.Precio));
                }
            }

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
            JornadaId = jornada.JornadaId,
            Comentario = ComentarioPedido.Normalizar(request.Dto.Comentario),
            DetallesPedidos = detalles
        };

        MesaOcupacion.Ocupar(mesa);

        _context.Pedidos.Add(pedido);
        await _context.SaveChangesAsync(cancellationToken);

        if (cambiosPrecio.Count > 0)
        {
            var quien = await PreciosPedido.UsuarioAsync(_context, request.UsuarioActualId ?? request.Dto.UsuarioId, cancellationToken);
            foreach (var c in cambiosPrecio)
                _context.CambiosPrecioPedido.Add(PreciosPedido.Registro(pedido, mesa, c.Detalle, c.Producto, c.PrecioCarta, c.Detalle.Monto, "Precio al tomar el pedido", quien));
            await _context.SaveChangesAsync(cancellationToken);
        }

        // Se registra antes de confirmar: un reintento que espera el bloqueo de la mesa ya lo encuentra.
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

        // Recargar con navegaciones para el DTO
        var pedidoCreado = await _context.Pedidos
            .Include(p => p.Mesa).ThenInclude(m => m.Salon)
            .Include(p => p.Usuario)
            .Include(p => p.DetallesPedidos)
                .ThenInclude(d => d.Producto)
            .FirstAsync(p => p.PedidoId == pedido.PedidoId, cancellationToken);

        await _hub.NotificarNuevoPedido(new NuevoPedidoEvent
        {
            PedidoId = pedidoCreado.PedidoId,
            MesaNumero = pedidoCreado.Mesa?.NumeroMesa ?? string.Empty,
            SalonNombre = pedidoCreado.Mesa?.Salon?.Nombre ?? string.Empty,
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
            SalonNombre = pedidoCreado.Mesa?.Salon?.Nombre ?? string.Empty,
            MozoNombre = pedidoCreado.Usuario?.NombreCompleto ?? string.Empty,
            CreadoEn = pedidoCreado.CreadoEn,
            EsAdicional = false,
            ParaCaja = request.ComandaEnCaja,
            Comentario = pedidoCreado.Comentario,
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
            SalonNombre = pedido.Mesa?.Salon?.Nombre ?? string.Empty,
            MesaNumero = pedido.Mesa?.NumeroMesa ?? string.Empty,
            UsuarioId = pedido.UsuarioId,
            UsuarioNombre = pedido.Usuario?.NombreCompleto ?? string.Empty,
            SubTotal = pedido.SubTotal,
            Total = pedido.Total,
            EstadoPedido = pedido.EstadoPedido,
            Comentario = pedido.Comentario,
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