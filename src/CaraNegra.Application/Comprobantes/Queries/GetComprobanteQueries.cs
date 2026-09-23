using CaraNegra.Application.Common;
using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Comprobantes.DTOs;
using CaraNegra.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Comprobantes.Queries;

// ---- Por ID ----
public record GetComprobanteByIdQuery(int ComprobanteId) : IRequest<ComprobanteDto>;

public class GetComprobanteByIdQueryHandler : IRequestHandler<GetComprobanteByIdQuery, ComprobanteDto>
{
    private readonly IApplicationDbContext _context;
    private readonly DatosEmpresa _empresa;

    public GetComprobanteByIdQueryHandler(IApplicationDbContext context, DatosEmpresa empresa)
    {
        _context = context;
        _empresa = empresa;
    }

    public async Task<ComprobanteDto> Handle(GetComprobanteByIdQuery request, CancellationToken cancellationToken)
    {
        var c = await ComprobanteQueries.ConDetalle(_context)
            .FirstOrDefaultAsync(x => x.ComprobanteId == request.ComprobanteId, cancellationToken)
            ?? throw new KeyNotFoundException($"Comprobante {request.ComprobanteId} no encontrado");
        return ComprobanteMapper.ToDto(c, _empresa);
    }
}

// ---- Por pedido (null si el pedido aún no tiene comprobante) ----
public record GetComprobanteByPedidoQuery(int PedidoId) : IRequest<ComprobanteDto?>;

public class GetComprobanteByPedidoQueryHandler : IRequestHandler<GetComprobanteByPedidoQuery, ComprobanteDto?>
{
    private readonly IApplicationDbContext _context;
    private readonly DatosEmpresa _empresa;

    public GetComprobanteByPedidoQueryHandler(IApplicationDbContext context, DatosEmpresa empresa)
    {
        _context = context;
        _empresa = empresa;
    }

    public async Task<ComprobanteDto?> Handle(GetComprobanteByPedidoQuery request, CancellationToken cancellationToken)
    {
        var c = await ComprobanteQueries.ConDetalle(_context)
            .Where(x => x.PedidoId == request.PedidoId && x.Estado != EstadoComprobante.Anulado)
            .OrderByDescending(x => x.ComprobanteId)
            .FirstOrDefaultAsync(cancellationToken);
        return c == null ? null : ComprobanteMapper.ToDto(c, _empresa);
    }
}

// ---- Listado (por rango de fechas de Lima y tipo) ----
public record GetAllComprobantesQuery(
    DateTime? FechaDesde = null,
    DateTime? FechaHasta = null,
    TipoComprobante? Tipo = null) : IRequest<ComprobantesListadoDto>;

public class ComprobantesListadoDto
{
    public List<ComprobanteResumenDto> Items { get; set; } = new();
    public int CantidadBoletas { get; set; }
    public int CantidadFacturas { get; set; }
    public decimal TotalOpGravada { get; set; }
    public decimal TotalIgv { get; set; }
    public decimal TotalVendido { get; set; }
}

public class ComprobanteResumenDto
{
    public int ComprobanteId { get; set; }
    public int PedidoId { get; set; }
    public TipoComprobante TipoComprobante { get; set; }
    public string Numero { get; set; } = string.Empty;
    public string ClienteNombre { get; set; } = string.Empty;
    public string? ClienteNumeroDocumento { get; set; }
    public decimal OpGravada { get; set; }
    public decimal Igv { get; set; }
    public decimal Total { get; set; }
    public DateTime FechaEmision { get; set; }
    public EstadoComprobante Estado { get; set; }
}

public class GetAllComprobantesQueryHandler : IRequestHandler<GetAllComprobantesQuery, ComprobantesListadoDto>
{
    private readonly IApplicationDbContext _context;

    public GetAllComprobantesQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<ComprobantesListadoDto> Handle(GetAllComprobantesQuery request, CancellationToken cancellationToken)
    {
        var desde = PeruDateRangeHelper.InicioDelDiaUtc(request.FechaDesde ?? PeruDateRangeHelper.ToHoraPeru(DateTime.UtcNow));
        var hasta = PeruDateRangeHelper.FinDelDiaUtc(request.FechaHasta ?? request.FechaDesde ?? PeruDateRangeHelper.ToHoraPeru(DateTime.UtcNow));

        var query = _context.Comprobantes
            .Where(c => c.FechaEmision >= desde && c.FechaEmision <= hasta);

        if (request.Tipo.HasValue)
            query = query.Where(c => c.TipoComprobante == request.Tipo.Value);

        var filas = await query
            .OrderByDescending(c => c.FechaEmision)
            .Select(c => new
            {
                c.ComprobanteId,
                c.PedidoId,
                c.TipoComprobante,
                c.Serie,
                c.Correlativo,
                c.ClienteNombre,
                c.ClienteNumeroDocumento,
                c.OpGravada,
                c.Igv,
                c.Total,
                c.FechaEmision,
                c.Estado
            })
            .ToListAsync(cancellationToken);

        var lista = filas.Select(c => new ComprobanteResumenDto
        {
            ComprobanteId = c.ComprobanteId,
            PedidoId = c.PedidoId,
            TipoComprobante = c.TipoComprobante,
            Numero = ComprobanteMapper.FormatearNumero(c.Serie, c.Correlativo),
            ClienteNombre = c.ClienteNombre,
            ClienteNumeroDocumento = c.ClienteNumeroDocumento,
            OpGravada = c.OpGravada,
            Igv = c.Igv,
            Total = c.Total,
            FechaEmision = c.FechaEmision,
            Estado = c.Estado
        }).ToList();

        var validos = lista.Where(c => c.Estado != EstadoComprobante.Anulado).ToList();

        return new ComprobantesListadoDto
        {
            Items = lista,
            CantidadBoletas = validos.Count(c => c.TipoComprobante == TipoComprobante.Boleta),
            CantidadFacturas = validos.Count(c => c.TipoComprobante == TipoComprobante.Factura),
            TotalOpGravada = validos.Sum(c => c.OpGravada),
            TotalIgv = validos.Sum(c => c.Igv),
            TotalVendido = validos.Sum(c => c.Total)
        };
    }
}
