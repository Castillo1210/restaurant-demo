using CaraNegra.Domain.Enums;

namespace CaraNegra.Application.Jornadas;

public class JornadaDto
{
    public int JornadaId { get; set; }
    public DateTime FechaOperativa { get; set; }
    public EstadoJornada Estado { get; set; }
    public DateTime AbiertaEn { get; set; }
    public string AbiertaPor { get; set; } = string.Empty;
    public decimal FondoInicial { get; set; }
    public string? ObservacionApertura { get; set; }
    public DateTime? CerradaEn { get; set; }
    public string? CerradaPor { get; set; }
    public string? ObservacionCierre { get; set; }

    /// <summary>Abierta y de un día anterior a hoy (se olvidaron de cerrarla).</summary>
    public bool EsDeDiaAnterior { get; set; }

    // Totales (solo jornadas cerradas)
    public decimal TotalVentas { get; set; }
    public int CantidadPedidos { get; set; }
    public decimal DiferenciaTotal { get; set; }
}

/// <summary>GET /jornadas/actual.</summary>
public class JornadaActualDto
{
    public bool Abierta { get; set; }
    public JornadaDto? Jornada { get; set; }
}

public class AbrirJornadaDto
{
    /// <summary>Opcional: por defecto, hoy (Lima).</summary>
    public DateTime? FechaOperativa { get; set; }
    public decimal FondoInicial { get; set; }
    public string? Observacion { get; set; }
}

public class RegistrarMovimientoDto
{
    public TipoMovimientoCaja Tipo { get; set; }
    public decimal Monto { get; set; }
    public string Motivo { get; set; } = string.Empty;
}

public class MovimientoCajaDto
{
    public int MovimientoCajaId { get; set; }
    public TipoMovimientoCaja Tipo { get; set; }
    public decimal Monto { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public string RegistradoPor { get; set; } = string.Empty;
    public DateTime CreadoEn { get; set; }
    public bool EstaAnulado { get; set; }
    public string? AnuladoPor { get; set; }
}

public class MetodoCierreDto
{
    public int? MetodoPagoId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool EsEfectivo { get; set; }
}

public class PedidoAbiertoDto
{
    public int PedidoId { get; set; }
    public string MesaNumero { get; set; } = string.Empty;
    public string SalonNombre { get; set; } = string.Empty;
    public string Mozo { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public decimal Pagado { get; set; }
}

/// <summary>Revisión previa al cierre (sin montos esperados: el arqueo es ciego).</summary>
public class PreCierreDto
{
    public JornadaDto Jornada { get; set; } = new();
    public List<PedidoAbiertoDto> PedidosAbiertos { get; set; } = new();
    public int ComprobantesPendientes { get; set; }
    public List<MetodoCierreDto> Metodos { get; set; } = new();
    public bool PuedeCerrar => PedidosAbiertos.Count == 0;
}

public class ConteoMetodoDto
{
    public int? MetodoPagoId { get; set; }
    public string? Nombre { get; set; }
    public decimal Contado { get; set; }
}

public class CerrarJornadaDto
{
    /// <summary>Obsoleto: el cierre ya no lleva arqueo (se cierra con lo registrado). Se ignora.</summary>
    public List<ConteoMetodoDto>? Conteos { get; set; }
    public string? Observacion { get; set; }
}

// ───────────── Resumen / reporte de cierre ─────────────

public class ResumenJornadaDto
{
    public JornadaDto Jornada { get; set; } = new();

    /// <summary>False cuando se ocultan los montos (cajero con la jornada aún abierta: arqueo ciego).</summary>
    public bool IncluyeMontos { get; set; }

    public int CantidadPedidos { get; set; }
    public int PedidosCancelados { get; set; }
    public decimal TotalVentas { get; set; }
    public decimal TicketPromedio { get; set; }
    public decimal TotalDescuentos { get; set; }
    public int PagosAnulados { get; set; }
    public decimal MontoPagosAnulados { get; set; }

    /// <summary>Productos quitados de pedidos en la jornada (quién, cuándo, por qué).</summary>
    public int ProductosAnulados { get; set; }
    public decimal MontoProductosAnulados { get; set; }
    public List<CaraNegra.Application.Pedidos.ProductoAnuladoDto> Anulaciones { get; set; } = new();

    /// <summary>Precios distintos a la carta (solo cajero / admin), con quién y cuándo.</summary>
    public List<CaraNegra.Application.Pedidos.CambioPrecioDto> CambiosPrecio { get; set; } = new();

    public List<LineaResumenDto> PorMetodo { get; set; } = new();
    public List<LineaResumenDto> PorMozo { get; set; } = new();
    public List<LineaResumenDto> PorSalon { get; set; } = new();
    public List<LineaResumenDto> TopProductos { get; set; } = new();

    public ResumenComprobantesDto Comprobantes { get; set; } = new();

    public List<MovimientoCajaDto> Movimientos { get; set; } = new();
    public decimal TotalIngresos { get; set; }
    public decimal TotalEgresos { get; set; }

    /// <summary>Con la jornada abierta: solo esperado. Cerrada: esperado, contado y diferencia.</summary>
    public List<ArqueoDto> Arqueo { get; set; } = new();
    public decimal DiferenciaTotal { get; set; }
}

public class LineaResumenDto
{
    public string Nombre { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal Total { get; set; }
}

public class ResumenComprobantesDto
{
    public int Boletas { get; set; }
    public string? RangoBoletas { get; set; }
    public int Facturas { get; set; }
    public string? RangoFacturas { get; set; }
    public decimal TotalFacturado { get; set; }
    public int PorConfirmar { get; set; }
    public int ConError { get; set; }
}

public class ArqueoDto
{
    public int? MetodoPagoId { get; set; }
    public string MetodoPago { get; set; } = string.Empty;
    public bool EsEfectivo { get; set; }
    public decimal Ventas { get; set; }
    public decimal Esperado { get; set; }
    public decimal? Contado { get; set; }
    public decimal? Diferencia { get; set; }
}
