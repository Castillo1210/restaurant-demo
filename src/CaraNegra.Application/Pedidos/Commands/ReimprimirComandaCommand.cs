using CaraNegra.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Pedidos.Commands;

/// <summary>
/// Reimpresión manual de la comanda completa de un pedido (Fase 6), para cuando la impresora
/// estuvo apagada/sin papel al momento de la impresión automática. Siempre se marca
/// EsAdicional = false porque reimprime el pedido completo, no un agregado puntual.
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
            .Include(p => p.Mesa)
            .Include(p => p.Usuario)
            .Include(p => p.DetallesPedidos)
                .ThenInclude(d => d.Producto)
            .FirstOrDefaultAsync(p => p.PedidoId == request.PedidoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Pedido {request.PedidoId} no encontrado");

        await _impresora.ImprimirComandaAsync(new ComandaCocina
        {
            PedidoId = pedido.PedidoId,
            MesaNumero = pedido.Mesa?.NumeroMesa ?? string.Empty,
            MozoNombre = pedido.Usuario?.NombreCompleto ?? string.Empty,
            CreadoEn = pedido.CreadoEn,
            EsAdicional = false,
            Items = pedido.DetallesPedidos.Select(d => new ItemComanda
            {
                ProductoNombre = d.Producto?.Nombre ?? string.Empty,
                Cantidad = d.Cantidad,
                Notas = d.Notas
            }).ToList()
        }, cancellationToken);

        return Unit.Value;
    }
}
