using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Pedidos;

/// <summary>Un producto quitado de un pedido (auditoría: quién, cuándo, por qué).</summary>
public class ProductoAnuladoDto
{
    public int ProductoAnuladoId { get; set; }
    public int PedidoId { get; set; }
    public string Mesa { get; set; } = string.Empty;
    public string ProductoNombre { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Importe { get; set; }
    public string EstadoItem { get; set; } = string.Empty;
    public string Motivo { get; set; } = string.Empty;
    public string UsuarioNombre { get; set; } = string.Empty;
    public string UsuarioRol { get; set; } = string.Empty;
    public DateTime AnuladoEn { get; set; }

    public static ProductoAnuladoDto Desde(ProductoAnulado a) => new()
    {
        ProductoAnuladoId = a.ProductoAnuladoId,
        PedidoId = a.PedidoId,
        Mesa = a.Mesa,
        ProductoNombre = a.ProductoNombre,
        Cantidad = a.Cantidad,
        PrecioUnitario = a.PrecioUnitario,
        Importe = a.Importe,
        EstadoItem = a.EstadoItem,
        Motivo = a.Motivo,
        UsuarioNombre = a.UsuarioNombre,
        UsuarioRol = a.UsuarioRol,
        AnuladoEn = a.AnuladoEn
    };
}

/// <summary>Productos quitados de un pedido, del más reciente al más antiguo.</summary>
public record GetAnulacionesPedidoQuery(int PedidoId) : IRequest<List<ProductoAnuladoDto>>;

public class GetAnulacionesPedidoQueryHandler : IRequestHandler<GetAnulacionesPedidoQuery, List<ProductoAnuladoDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAnulacionesPedidoQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<ProductoAnuladoDto>> Handle(GetAnulacionesPedidoQuery request, CancellationToken cancellationToken)
    {
        var lista = await _context.ProductosAnulados
            .AsNoTracking()
            .Where(a => a.PedidoId == request.PedidoId)
            .OrderByDescending(a => a.AnuladoEn)
            .ToListAsync(cancellationToken);
        return lista.Select(ProductoAnuladoDto.Desde).ToList();
    }
}
