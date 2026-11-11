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
    private readonly IDatosEmpresaProvider _empresa;

    public GetComprobanteByIdQueryHandler(IApplicationDbContext context, IDatosEmpresaProvider empresa)
    {
        _context = context;
        _empresa = empresa;
    }

    public async Task<ComprobanteDto> Handle(GetComprobanteByIdQuery request, CancellationToken cancellationToken)
    {
        var c = await ComprobanteQueries.ConDetalle(_context)
            .FirstOrDefaultAsync(x => x.ComprobanteId == request.ComprobanteId, cancellationToken)
            ?? throw new KeyNotFoundException($"Comprobante {request.ComprobanteId} no encontrado");
        return ComprobanteMapper.ToDto(c, await _empresa.ObtenerAsync(cancellationToken));
    }
}

// ---- Por pedido (null si el pedido aún no tiene comprobante) ----
public record GetComprobanteByPedidoQuery(int PedidoId) : IRequest<ComprobanteDto?>;

public class GetComprobanteByPedidoQueryHandler : IRequestHandler<GetComprobanteByPedidoQuery, ComprobanteDto?>
{
    private readonly IApplicationDbContext _context;
    private readonly IDatosEmpresaProvider _empresa;

    public GetComprobanteByPedidoQueryHandler(IApplicationDbContext context, IDatosEmpresaProvider empresa)
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
        return c == null ? null : ComprobanteMapper.ToDto(c, await _empresa.ObtenerAsync(cancellationToken));
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
    public string? MensajeSunat { get; set; }
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
                c.Estado,
                c.MensajeSunat
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
            Estado = c.Estado,
            MensajeSunat = c.MensajeSunat
        }).ToList();

        // Rechazados y con error de envío no son comprobantes válidos: no suman.
        var validos = lista.Where(c => !ComprobanteEnvio.NoEsValido(c.Estado)).ToList();

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

// ---- Atrasados: emitidos hace rato y todavía sin confirmar en SUNAT (aviso en caja) ----
// Se separan por causa, porque se atienden distinto:
//   · Sin enviar (PorEnviar): no llegaron al proveedor → internet de esta PC o proveedor caído.
//   · Esperando a SUNAT (Pendiente): el proveedor ya los firmó y envió → la demora es de SUNAT.
public record GetComprobantesAtrasadosQuery(int MinutosSinConfirmar = 60, int MinutosSinEnviar = 15)
    : IRequest<ComprobantesAtrasadosDto>;

// ---- Próximo número de una serie (para que el administrador lo vea antes de emitir) ----
public record GetProximoNumeroQuery(string Serie) : IRequest<ProximoNumeroDto>;

public class ProximoNumeroDto
{
    public string Serie { get; set; } = string.Empty;
    public int Numero { get; set; }
    /// <summary>Como sale impreso: BE01-00012199.</summary>
    public string Texto { get; set; } = string.Empty;
}

public class GetProximoNumeroQueryHandler : IRequestHandler<GetProximoNumeroQuery, ProximoNumeroDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IFacturacionElectronicaService _facturacion;

    public GetProximoNumeroQueryHandler(IApplicationDbContext context, IFacturacionElectronicaService facturacion)
    {
        _context = context;
        _facturacion = facturacion;
    }

    public async Task<ProximoNumeroDto> Handle(GetProximoNumeroQuery request, CancellationToken cancellationToken)
    {
        var serie = (request.Serie ?? string.Empty).Trim().ToUpperInvariant();
        if (!System.Text.RegularExpressions.Regex.IsMatch(serie, "^[BF][A-Z0-9]{3}$"))
            throw new ArgumentException("La serie debe tener 4 caracteres y empezar con B o F (ej. B001).");

        var numero = await Numeracion.UltimoAsync(_context, _facturacion, serie, cancellationToken) + 1;
        return new ProximoNumeroDto { Serie = serie, Numero = numero, Texto = ComprobanteMapper.FormatearNumero(serie, numero) };
    }
}

public class ComprobantesAtrasadosDto
{
    /// <summary>Total de los dos grupos (compatibilidad).</summary>
    public int Cantidad { get; set; }
    public DateTime? MasAntiguo { get; set; }

    /// <summary>No llegaron al proveedor de facturación (PorEnviar).</summary>
    public int SinEnviar { get; set; }
    public DateTime? SinEnviarDesde { get; set; }

    /// <summary>El proveedor ya los recibió y firmó; falta la respuesta de SUNAT (Pendiente).</summary>
    public int EsperandoSunat { get; set; }
    public DateTime? EsperandoSunatDesde { get; set; }
}

public class GetComprobantesAtrasadosQueryHandler : IRequestHandler<GetComprobantesAtrasadosQuery, ComprobantesAtrasadosDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IFacturacionElectronicaService _facturacion;

    public GetComprobantesAtrasadosQueryHandler(IApplicationDbContext context, IFacturacionElectronicaService facturacion)
    {
        _context = context;
        _facturacion = facturacion;
    }

    public async Task<ComprobantesAtrasadosDto> Handle(GetComprobantesAtrasadosQuery request, CancellationToken cancellationToken)
    {
        if (_facturacion.EsSimulado) return new ComprobantesAtrasadosDto();

        var ambiente = _facturacion.Ambiente;
        var ahora = DateTime.UtcNow;
        var limiteSunat = ahora.AddMinutes(-Math.Max(1, request.MinutosSinConfirmar));
        var limiteEnvio = ahora.AddMinutes(-Math.Max(1, request.MinutosSinEnviar));

        var sinEnviar = _context.Comprobantes.Where(c =>
            c.Estado == EstadoComprobante.PorEnviar && c.Ambiente == ambiente && c.FechaEmision < limiteEnvio);
        var esperandoSunat = _context.Comprobantes.Where(c =>
            c.Estado == EstadoComprobante.Pendiente && c.Ambiente == ambiente && c.FechaEmision < limiteSunat);

        var dto = new ComprobantesAtrasadosDto
        {
            SinEnviar = await sinEnviar.CountAsync(cancellationToken),
            SinEnviarDesde = await sinEnviar.MinAsync(c => (DateTime?)c.FechaEmision, cancellationToken),
            EsperandoSunat = await esperandoSunat.CountAsync(cancellationToken),
            EsperandoSunatDesde = await esperandoSunat.MinAsync(c => (DateTime?)c.FechaEmision, cancellationToken)
        };
        dto.Cantidad = dto.SinEnviar + dto.EsperandoSunat;
        dto.MasAntiguo = new[] { dto.SinEnviarDesde, dto.EsperandoSunatDesde }.Where(f => f.HasValue).Min();
        return dto;
    }
}
