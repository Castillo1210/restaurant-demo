using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Pedidos.Commands;

/// <summary>
/// Reimpresión manual de la comanda completa de un pedido (Fase 6), para cuando la impresora
/// estuvo apagada/sin papel al momento de la impresión automática. Sale rotulada
/// "REIMPRESION" (para que cocina no la tome como pedido nuevo), solo con los platos vigentes,
/// y no se permite en pedidos cobrados o anulados. Si la impresora no responde, falla con un
/// mensaje claro en vez de decir "reenviada".
/// </summary>
public record ReimprimirComandaCommand(int PedidoId) : IRequest<Unit>;

public class ReimprimirComandaCommandHandler : IRequestHandler<ReimprimirComandaCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IImpresoraCocinaService _impresora;

    public ReimprimirComandaCommandHandler(IApplicationDbContext context, IImpresoraCocinaService impresora)
    {
        _context = context;
        _impresora = impresora;
    }

    public async Task<Unit> Handle(ReimprimirComandaCommand request, CancellationToken cancellationToken)
    {
        var pedido = await _context.Pedidos
            .Include(p => p.Mesa).ThenInclude(m => m.Salon)
            .Include(p => p.Usuario)
            .Include(p => p.DetallesPedidos)
                .ThenInclude(d => d.Producto)
            .FirstOrDefaultAsync(p => p.PedidoId == request.PedidoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Pedido {request.PedidoId} no encontrado");

        if (pedido.EstadoPedido is EstadoPedido.Cancelado or EstadoPedido.Entregado)
            throw new InvalidOperationException("Este pedido ya fue cobrado o anulado: no se reimprime la comanda.");

        var resultado = await _impresora.ImprimirComandaAsync(new ComandaCocina
        {
            PedidoId = pedido.PedidoId,
            MesaNumero = pedido.Mesa?.NumeroMesa ?? string.Empty,
            SalonNombre = pedido.Mesa?.Salon?.Nombre ?? string.Empty,
            MozoNombre = pedido.Usuario?.NombreCompleto ?? string.Empty,
            CreadoEn = pedido.CreadoEn,
            EsAdicional = false,
            EsReimpresion = true,
            Comentario = pedido.Comentario,
            Items = pedido.DetallesPedidos
                .Where(d => d.EstadoDetallePedido != EstadoDetallePedido.Cancelado)
                .Select(d => new ItemComanda
            {
                ProductoNombre = d.Producto?.Nombre ?? string.Empty,
                Cantidad = d.Cantidad,
                Notas = d.Notas
            }).ToList()
        }, cancellationToken);

        return resultado switch
        {
            ResultadoImpresion.Fallo => throw new InvalidOperationException(
                "La impresora de cocina no respondió: revisa que esté encendida, con papel y conectada, y vuelve a intentar."),
            ResultadoImpresion.Deshabilitada => throw new InvalidOperationException(
                "La impresora de cocina está desactivada en la configuración: no se imprimió nada."),
            _ => Unit.Value
        };
    }
}
