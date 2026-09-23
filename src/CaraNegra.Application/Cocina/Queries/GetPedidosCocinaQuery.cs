using CaraNegra.Application.Cocina.DTOs;
using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Domain.Entities;
using CaraNegra.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Cocina.Queries;

/// <summary>
/// Pedidos activos para la pantalla de cocina: Pendiente, En preparación y Listo (hasta que se
/// cobran). Los ítems cancelados no se muestran.
/// </summary>
public record GetPedidosCocinaQuery : IRequest<List<PedidoCocinaDto>>;

public class GetPedidosCocinaQueryHandler : IRequestHandler<GetPedidosCocinaQuery, List<PedidoCocinaDto>>
{
    private static readonly EstadoPedido[] EstadosVisibles =
    {
        EstadoPedido.Pendiente, EstadoPedido.EnPreparacion, EstadoPedido.Listo
    };

    private readonly IApplicationDbContext _context;

    public GetPedidosCocinaQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<PedidoCocinaDto>> Handle(GetPedidosCocinaQuery request, CancellationToken cancellationToken)
    {
        var pedidos = await _context.Pedidos
            .Include(p => p.Mesa)
            .Include(p => p.Usuario)
            .Include(p => p.DetallesPedidos)
                .ThenInclude(d => d.Producto)
                    .ThenInclude(pr => pr.Categoria)
            .Where(p => EstadosVisibles.Contains(p.EstadoPedido))
            .OrderBy(p => p.CreadoEn)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return pedidos.Select(Map).Where(p => p.Items.Count > 0).ToList();
    }

    public static PedidoCocinaDto Map(Pedido p) => new()
    {
        PedidoId = p.PedidoId,
        MesaNumero = p.Mesa?.NumeroMesa ?? string.Empty,
        MozoNombre = p.Usuario?.NombreCompleto ?? string.Empty,
        EstadoPedido = p.EstadoPedido,
        CreadoEn = p.CreadoEn,
        ActualizadoEn = p.ActualizadoEn,
        Items = p.DetallesPedidos
            .Where(d => d.EstadoDetallePedido != EstadoDetallePedido.Cancelado)
            .OrderBy(d => d.CreadoEn)
            .Select(d => new ItemCocinaDto
            {
                DetallePedidoId = d.DetallePedidoId,
                ProductoId = d.ProductoId,
                ProductoNombre = d.Producto?.Nombre ?? string.Empty,
                CategoriaNombre = d.Producto?.Categoria?.Nombre,
                Cantidad = d.Cantidad,
                Notas = d.Notas,
                Estado = d.EstadoDetallePedido,
                CreadoEn = d.CreadoEn
            })
            .ToList()
    };
}
