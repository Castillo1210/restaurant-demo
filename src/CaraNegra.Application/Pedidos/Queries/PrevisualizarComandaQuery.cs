using CaraNegra.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Pedidos.Queries;

/// <summary>
/// Devuelve el texto exacto de la comanda que se enviaría a la impresora térmica para un
/// pedido, sin imprimir nada de verdad. Pensado para que el usuario pueda ver el formato
/// del ticket (por ejemplo, para presentar el sistema al cliente) sin necesitar tener la
/// impresora física conectada.
/// </summary>
public record PrevisualizarComandaQuery(int PedidoId) : IRequest<string>;

public class PrevisualizarComandaQueryHandler : IRequestHandler<PrevisualizarComandaQuery, string>
{
    private readonly IApplicationDbContext _context;
    private readonly IImpresoraCocinaService _impresora;

    public PrevisualizarComandaQueryHandler(IApplicationDbContext context, IImpresoraCocinaService impresora)
    {
        _context = context;
        _impresora = impresora;
    }

    public async Task<string> Handle(PrevisualizarComandaQuery request, CancellationToken cancellationToken)
    {
        var pedido = await _context.Pedidos
            .Include(p => p.Mesa)
            .Include(p => p.Usuario)
            .Include(p => p.DetallesPedidos)
                .ThenInclude(d => d.Producto)
            .FirstOrDefaultAsync(p => p.PedidoId == request.PedidoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Pedido {request.PedidoId} no encontrado");

        return _impresora.PrevisualizarComanda(new ComandaCocina
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
        });
    }
}
