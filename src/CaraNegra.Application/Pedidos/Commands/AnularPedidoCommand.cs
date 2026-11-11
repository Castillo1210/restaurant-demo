using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Mesas;
using CaraNegra.Application.Pedidos.DTOs;
using CaraNegra.Application.Pedidos.Queries;
using CaraNegra.Domain.Entities;
using CaraNegra.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Pedidos.Commands;

/// <summary>
/// Anula un pedido COMPLETO (el cliente se fue, se tomó en la mesa equivocada…). Igual que
/// quitar un plato: motivo obligatorio, cada plato queda registrado en ProductosAnulados con
/// quién, cuándo y por qué, y a cocina le sale un aviso "PEDIDO ANULADO - NO PREPARAR".
/// La mesa queda libre si no tiene otro pedido abierto.
/// También se usa al quitar el ÚLTIMO plato de un pedido (ver EliminarDetallePedidoCommand).
/// </summary>
/// <param name="SoloPedidosPropios">true para el rol MOZO: solo en las mesas que él atiende.</param>
/// <param name="AvisarCocina">
/// false = no imprime el ticket "PEDIDO ANULADO" (quien anula sabe que la cocina no empezó esos platos).
/// Por defecto se imprime: la cocina trabaja con los tickets y es su única forma de enterarse.
/// </param>
/// <param name="ImprimeEnCaja">Anula el administrador o el cajero: el aviso lo imprime su propio equipo, no la ticketera de cocina.</param>
public record AnularPedidoCommand(int PedidoId, int UsuarioId, string? Motivo, bool SoloPedidosPropios = false,
    bool AvisarCocina = true, bool ImprimeEnCaja = false)
    : IRequest<PedidoDto>;

public class AnularPedidoCommandHandler : IRequestHandler<AnularPedidoCommand, PedidoDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IPedidosHubService _hub;
    private readonly IImpresoraCocinaService _impresora;
    private readonly IMediator _mediator;

    public AnularPedidoCommandHandler(IApplicationDbContext context, IPedidosHubService hub,
        IImpresoraCocinaService impresora, IMediator mediator)
    {
        _context = context;
        _hub = hub;
        _impresora = impresora;
        _mediator = mediator;
    }

    public async Task<PedidoDto> Handle(AnularPedidoCommand request, CancellationToken cancellationToken)
    {
        var motivo = AnulacionPedido.ValidarMotivo(request.Motivo);

        // Mismo orden de bloqueo que el resto de comandos del pedido (dos personas a la vez).
        await using var tx = await _context.IniciarTransaccionAsync(cancellationToken);
        await _context.BloquearPedidoAsync(request.PedidoId, cancellationToken);

        var pedido = await _context.Pedidos
            .Include(p => p.Mesa).ThenInclude(m => m.Salon)
            .Include(p => p.Usuario)
            .Include(p => p.DetallesPedidos).ThenInclude(d => d.Producto)
            .Include(p => p.Pagos)
            .FirstOrDefaultAsync(p => p.PedidoId == request.PedidoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Pedido {request.PedidoId} no encontrado");

        if (pedido.EstadoPedido is EstadoPedido.Cancelado or EstadoPedido.Entregado)
            throw new InvalidOperationException("El pedido ya fue cobrado o anulado.");

        // Igual que al quitar platos: con el pedido ya "Listo" solo el cajero o el admin.
        if (request.SoloPedidosPropios && pedido.EstadoPedido == EstadoPedido.Listo)
            throw new InvalidOperationException("El pedido ya está listo: solo el cajero o el administrador pueden anularlo.");

        if (request.SoloPedidosPropios && pedido.UsuarioId != request.UsuarioId)
            throw new InvalidOperationException(
                $"Esta mesa la atiende {pedido.Usuario?.NombreCompleto ?? "otro mozo"}: solo él (o el cajero / admin) puede anular el pedido.");

        var usuario = await _context.Usuarios.AsNoTracking()
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.UsuarioId == request.UsuarioId, cancellationToken)
            ?? throw new KeyNotFoundException("Usuario no encontrado");

        var resultado = await AnulacionPedido.AplicarAsync(_context, pedido, usuario, motivo, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        await tx.ConfirmarAsync(cancellationToken);

        await AnulacionPedido.NotificarAsync(_hub, _impresora, pedido, usuario.NombreCompleto, motivo, resultado, cancellationToken,
            imprimirEnCocina: request.AvisarCocina, paraCaja: request.ImprimeEnCaja);

        return await _mediator.Send(new GetPedidoByIdQuery(pedido.PedidoId), cancellationToken);
    }
}

/// <summary>Lógica común para anular un pedido completo (botón "Anular pedido" y quitar el último plato).</summary>
public static class AnulacionPedido
{
    public sealed record Resultado(EstadoPedido EstadoAnterior, bool MesaLiberada, List<ItemComanda> Items);

    public static string ValidarMotivo(string? motivo)
    {
        var limpio = (motivo ?? string.Empty).Trim();
        if (limpio.Length < EliminarDetallePedidoCommandHandler.MotivoMinimo)
            throw new InvalidOperationException("Indica el motivo para anular (queda registrado).");
        return limpio.Length > EliminarDetallePedidoCommandHandler.MotivoMaximo
            ? limpio[..EliminarDetallePedidoCommandHandler.MotivoMaximo]
            : limpio;
    }

    /// <summary>
    /// Marca el pedido y sus platos como anulados, registra cada plato en ProductosAnulados y libera
    /// la mesa si no tiene otro pedido abierto. Debe llamarse dentro de la transacción del pedido;
    /// el que llama hace SaveChanges y después <see cref="NotificarAsync"/>.
    /// </summary>
    public static async Task<Resultado> AplicarAsync(IApplicationDbContext context, Pedido pedido, Usuario usuario,
        string motivo, CancellationToken cancellationToken)
    {
        if (pedido.Pagos.Any(p => !p.EstaAnulado))
            throw new InvalidOperationException(
                "Este pedido ya tiene un pago registrado: primero anula el pago en Caja y luego anula el pedido.");

        var mesaTexto = EtiquetaMesa(pedido.Mesa?.Salon?.Nombre, pedido.Mesa?.NumeroMesa);
        var ahora = DateTime.UtcNow;
        var items = new List<ItemComanda>();

        foreach (var detalle in pedido.DetallesPedidos.Where(d =>
                     d.EstadoDetallePedido != EstadoDetallePedido.Cancelado && d.EstadoDetallePedido != EstadoDetallePedido.Entregado))
        {
            var nombre = detalle.Producto?.Nombre ?? "Producto";
            context.ProductosAnulados.Add(new ProductoAnulado
            {
                PedidoId = pedido.PedidoId,
                JornadaId = pedido.JornadaId,
                ProductoId = detalle.ProductoId,
                ProductoNombre = nombre,
                Cantidad = detalle.Cantidad,
                PrecioUnitario = detalle.Monto,
                Importe = detalle.Monto * detalle.Cantidad,
                EstadoItem = detalle.EstadoDetallePedido.ToString(),
                Motivo = motivo,
                Mesa = mesaTexto,
                UsuarioId = usuario.UsuarioId,
                UsuarioNombre = usuario.NombreCompleto,
                UsuarioRol = usuario.Rol?.Nombre ?? string.Empty,
                AnuladoEn = ahora
            });
            items.Add(new ItemComanda { ProductoNombre = nombre, Cantidad = detalle.Cantidad });
            detalle.EstadoDetallePedido = EstadoDetallePedido.Cancelado;
        }

        var estadoAnterior = pedido.EstadoPedido;
        pedido.EstadoPedido = EstadoPedido.Cancelado;

        var mesaLiberada = await MesaOcupacion.LiberarSiQuedaLibreAsync(context, pedido.Mesa, pedido.PedidoId, cancellationToken);

        return new Resultado(estadoAnterior, mesaLiberada, items);
    }

    /// <summary>
    /// Avisa a las pantallas (SignalR) y, si <paramref name="imprimirEnCocina"/>, a la ticketera de cocina.
    /// Nunca lanza por la impresora.
    /// </summary>
    public static async Task NotificarAsync(IPedidosHubService hub, IImpresoraCocinaService impresora, Pedido pedido,
        string quienAnulo, string motivo, Resultado resultado, CancellationToken cancellationToken,
        bool imprimirEnCocina = true, bool paraCaja = false)
    {
        await hub.NotificarPedidoEstadoCambiado(new PedidoEstadoCambiadoEvent
        {
            PedidoId = pedido.PedidoId,
            MesaNumero = pedido.Mesa?.NumeroMesa ?? string.Empty,
            SalonNombre = pedido.Mesa?.Salon?.Nombre ?? string.Empty,
            EstadoAnterior = resultado.EstadoAnterior.ToString(),
            EstadoNuevo = EstadoPedido.Cancelado.ToString(),
            ActualizadoEn = DateTime.UtcNow
        });

        if (resultado.MesaLiberada && pedido.Mesa != null)
        {
            await hub.NotificarMesaEstadoCambiado(new MesaEstadoCambiadoEvent
            {
                MesaId = pedido.Mesa.MesaId,
                NumeroMesa = pedido.Mesa.NumeroMesa,
                EstadoAnterior = EstadoMesa.Ocupada.ToString(),
                EstadoNuevo = EstadoMesa.Disponible.ToString()
            });
        }

        if (imprimirEnCocina && resultado.Items.Count > 0)
        {
            await impresora.ImprimirComandaAsync(new ComandaCocina
            {
                PedidoId = pedido.PedidoId,
                MesaNumero = pedido.Mesa?.NumeroMesa ?? string.Empty,
                SalonNombre = pedido.Mesa?.Salon?.Nombre ?? string.Empty,
                MozoNombre = quienAnulo,
                CreadoEn = DateTime.UtcNow,
                EsAnulacion = true,
                EsPedidoCompleto = true,
                Motivo = motivo,
                ParaCaja = paraCaja,
                Items = resultado.Items
            }, cancellationToken);
        }
    }

    /// <summary>"Terraza 4": primera palabra del salón + número (igual que en las pantallas).</summary>
    public static string EtiquetaMesa(string? salon, string? numero)
    {
        var prefijo = (salon ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return $"{prefijo ?? "Mesa"} {numero}".Trim();
    }
}
