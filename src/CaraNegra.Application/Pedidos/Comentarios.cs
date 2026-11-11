using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Pedidos.DTOs;
using CaraNegra.Application.Pedidos.Queries;
using CaraNegra.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Pedidos;

/// <summary>
/// Comentario general de un pedido ("todo para llevar", "cliente apurado", "servir juntos").
/// Es distinto de las notas de cada plato ("sin cebolla"): va una sola vez por pedido, sale
/// destacado en la comanda de cocina y se ve en la pantalla de cocina y en caja.
/// </summary>
public static class ComentarioPedido
{
    public const int Maximo = 300;

    /// <summary>Quita espacios sobrantes; vacío → null. Lanza si supera el máximo.</summary>
    public static string? Normalizar(string? comentario)
    {
        if (string.IsNullOrWhiteSpace(comentario)) return null;
        // Varias líneas o espacios dobles → una sola línea limpia (así se imprime bien).
        var limpio = string.Join(' ', comentario.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (limpio.Length > Maximo)
            throw new InvalidOperationException($"El comentario del pedido admite hasta {Maximo} caracteres");
        return limpio;
    }
}

/// <summary>
/// Nota de UN plato del pedido ("sin cebolla", "término medio"): va en su línea, sale debajo del
/// plato en la comanda y junto al plato en la pantalla de cocina.
/// </summary>
public static class NotaPlato
{
    public const int Maximo = 200;

    /// <summary>Quita espacios sobrantes; vacío → null. Lanza si supera el máximo.</summary>
    public static string? Normalizar(string? nota)
    {
        if (string.IsNullOrWhiteSpace(nota)) return null;
        var limpia = string.Join(' ', nota.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (limpia.Length > Maximo)
            throw new InvalidOperationException($"La nota de un plato admite hasta {Maximo} caracteres");
        return limpia;
    }
}

/// <param name="UsuarioId">Quien cambia el comentario (sale en el ticket de cocina).</param>
/// <param name="SoloPedidosPropios">true para el rol MOZO: solo en las mesas que él atiende.</param>
public record ActualizarComentarioPedidoCommand(int PedidoId, string? Comentario, int UsuarioId, bool SoloPedidosPropios = false)
    : IRequest<PedidoDto>;

public class ActualizarComentarioPedidoCommandHandler : IRequestHandler<ActualizarComentarioPedidoCommand, PedidoDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IPedidosHubService _hub;
    private readonly IImpresoraCocinaService _impresora;
    private readonly IMediator _mediator;

    public ActualizarComentarioPedidoCommandHandler(IApplicationDbContext context, IPedidosHubService hub,
        IImpresoraCocinaService impresora, IMediator mediator)
    {
        _context = context;
        _hub = hub;
        _impresora = impresora;
        _mediator = mediator;
    }

    public async Task<PedidoDto> Handle(ActualizarComentarioPedidoCommand request, CancellationToken cancellationToken)
    {
        var nuevo = ComentarioPedido.Normalizar(request.Comentario);

        // Mismo orden de bloqueo que el resto de comandos del pedido (dos personas editando a la vez).
        await using var tx = await _context.IniciarTransaccionAsync(cancellationToken);
        await _context.BloquearPedidoAsync(request.PedidoId, cancellationToken);

        var pedido = await _context.Pedidos
            .Include(p => p.Mesa).ThenInclude(m => m.Salon)
            .Include(p => p.Usuario)
            .FirstOrDefaultAsync(p => p.PedidoId == request.PedidoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Pedido {request.PedidoId} no encontrado");

        if (pedido.EstadoPedido is EstadoPedido.Cancelado or EstadoPedido.Entregado)
            throw new InvalidOperationException("El pedido ya fue cobrado o cancelado: no se puede cambiar el comentario");

        if (request.SoloPedidosPropios && pedido.UsuarioId != request.UsuarioId)
            throw new InvalidOperationException(
                $"Esta mesa la atiende {pedido.Usuario?.NombreCompleto ?? "otro mozo"}: solo él (o el cajero / admin) puede cambiar el comentario.");

        if (string.Equals(pedido.Comentario, nuevo, StringComparison.Ordinal))
            return await _mediator.Send(new GetPedidoByIdQuery(pedido.PedidoId), cancellationToken);

        pedido.Comentario = nuevo;
        await _context.SaveChangesAsync(cancellationToken);
        await tx.ConfirmarAsync(cancellationToken);

        await _hub.NotificarPedidoActualizado(new PedidoActualizadoEvent
        {
            PedidoId = pedido.PedidoId,
            MesaNumero = pedido.Mesa?.NumeroMesa ?? string.Empty,
            SubTotal = pedido.SubTotal,
            Total = pedido.Total
        });

        // Cocina se entera del comentario nuevo con un ticket corto (solo el comentario, sin platos).
        // Si se borró el comentario no se imprime nada. Nunca lanza (ver IImpresoraCocinaService).
        if (nuevo != null)
        {
            var quien = await _context.Usuarios.AsNoTracking()
                .Where(u => u.UsuarioId == request.UsuarioId)
                .Select(u => u.NombreCompleto)
                .FirstOrDefaultAsync(cancellationToken);

            await _impresora.ImprimirComandaAsync(new ComandaCocina
            {
                PedidoId = pedido.PedidoId,
                MesaNumero = pedido.Mesa?.NumeroMesa ?? string.Empty,
                SalonNombre = pedido.Mesa?.Salon?.Nombre ?? string.Empty,
                MozoNombre = quien ?? pedido.Usuario?.NombreCompleto ?? string.Empty,
                CreadoEn = DateTime.UtcNow,
                Comentario = nuevo,
                EsCambioComentario = true
            }, cancellationToken);
        }

        return await _mediator.Send(new GetPedidoByIdQuery(pedido.PedidoId), cancellationToken);
    }
}
