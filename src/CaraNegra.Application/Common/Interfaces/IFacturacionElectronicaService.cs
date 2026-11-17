using CaraNegra.Domain.Enums;

namespace CaraNegra.Application.Common.Interfaces;

/// <summary>
/// Proveedor de facturación electrónica (PSE). La implementación vive en la API:
///   - "Simulado": no sale a internet (demo).
///   - "ApiSunat": APISUNAT (Lucode), en sandbox o producción.
/// La aplicación solo arma la solicitud con los datos del comprobante y guarda la respuesta.
/// </summary>
public interface IFacturacionElectronicaService
{
    /// <summary>"Simulado" o "ApiSunat".</summary>
    string Proveedor { get; }

    /// <summary>SIMULADO, SANDBOX o PRODUCCION.</summary>
    string Ambiente { get; }

    /// <summary>True si no hay envío real a un proveedor (demo).</summary>
    bool EsSimulado { get; }

    /// <summary>
    /// Último número emitido en <paramref name="serie"/> con el sistema anterior (configuración
    /// FacturacionElectronica:UltimoNumeroPorSerie); 0 si no se configuró.
    /// </summary>
    int UltimoNumeroAnterior(string serie);

    Task<RespuestaFacturacion> EnviarAsync(SolicitudComprobante solicitud, CancellationToken cancellationToken = default);

    Task<RespuestaFacturacion> ConsultarEstadoAsync(TipoComprobante tipo, string serie, int numero, CancellationToken cancellationToken = default);

    // ── Anulaciones y notas de crédito ──

    /// <summary>Serie de las notas de crédito que afectan a ese tipo de comprobante (FC01 / BC01).</summary>
    string SerieNotaCredito(TipoComprobante afectado);

    /// <summary>
    /// Anula dentro del plazo: factura → comunicación de baja (POST /voided);
    /// boleta → resumen diario con acción "anular" (POST /daily-summary).
    /// </summary>
    Task<RespuestaFacturacion> AnularAsync(SolicitudAnulacion solicitud, CancellationToken cancellationToken = default);

    /// <summary>Emite una nota de crédito (la solicitud lleva <see cref="SolicitudComprobante.NotaCredito"/>).</summary>
    Task<RespuestaFacturacion> EmitirNotaCreditoAsync(SolicitudComprobante solicitud, CancellationToken cancellationToken = default);

    Task<RespuestaFacturacion> ConsultarNotaCreditoAsync(string serie, int numero, CancellationToken cancellationToken = default);
}

/// <summary>Comunicación de baja (factura) o anulación en resumen diario (boleta).</summary>
public class SolicitudAnulacion
{
    public TipoComprobante Tipo { get; set; }
    public string Serie { get; set; } = string.Empty;
    public int Numero { get; set; }
    public string Motivo { get; set; } = string.Empty;
}

/// <summary>Datos propios de una nota de crédito.</summary>
public class DatosNotaCredito
{
    /// <summary>Catálogo 09: "01" anulación de la operación, "03" corrección por error en la descripción.</summary>
    public string Codigo { get; set; } = "01";
    public string Motivo { get; set; } = string.Empty;
    public TipoComprobante AfectadoTipo { get; set; }
    public string AfectadoSerie { get; set; } = string.Empty;
    public int AfectadoNumero { get; set; }
}

/// <summary>Datos del comprobante que se envían al proveedor (independiente del formato del PSE).</summary>
public class SolicitudComprobante
{
    public TipoComprobante Tipo { get; set; }
    public string Serie { get; set; } = string.Empty;
    public int Numero { get; set; }

    /// <summary>Fecha y hora de emisión en hora de Lima.</summary>
    public DateTime FechaEmisionLocal { get; set; }

    public TipoDocumentoIdentidad ClienteTipoDocumento { get; set; }
    public string? ClienteNumeroDocumento { get; set; }
    public string ClienteNombre { get; set; } = string.Empty;
    public string? ClienteDireccion { get; set; }

    public decimal IgvPorcentaje { get; set; }

    /// <summary>Importe total cobrado (con IGV y con el descuento ya aplicado).</summary>
    public decimal Total { get; set; }

    public List<ItemSolicitud> Items { get; set; } = new();

    /// <summary>Si no es null, se emite una NOTA DE CRÉDITO (Serie/Numero son los de la nota).</summary>
    public DatosNotaCredito? NotaCredito { get; set; }
}

public class ItemSolicitud
{
    public string Descripcion { get; set; } = string.Empty;
    public int Cantidad { get; set; }

    /// <summary>Precio de carta por unidad, CON IGV.</summary>
    public decimal PrecioUnitario { get; set; }
}

public enum ResultadoFacturacion
{
    /// <summary>SUNAT lo aceptó (hay CDR).</summary>
    Aceptado,
    /// <summary>Firmado y enviado, SUNAT aún no responde.</summary>
    Pendiente,
    /// <summary>SUNAT lo rechazó por datos incorrectos.</summary>
    Rechazado,
    /// <summary>El proveedor no aceptó los datos (validación). No llegó a SUNAT.</summary>
    ErrorDatos,
    /// <summary>El número ya existe en el proveedor.</summary>
    Duplicado,
    /// <summary>Consulta de estado: el proveedor no tiene ese comprobante.</summary>
    NoEncontrado,
    /// <summary>No hubo respuesta clara (sin internet, timeout, 5xx, token inválido…). Se puede reintentar.</summary>
    SinConexion,
    /// <summary>Consulta de estado: el comprobante figura anulado / dado de baja.</summary>
    Anulado
}

public class RespuestaFacturacion
{
    public ResultadoFacturacion Resultado { get; set; }
    public string? Mensaje { get; set; }
    public string? Hash { get; set; }
    public string? XmlUrl { get; set; }
    public string? CdrUrl { get; set; }
    public string? PdfUrl { get; set; }

    public static RespuestaFacturacion De(ResultadoFacturacion resultado, string? mensaje = null) =>
        new() { Resultado = resultado, Mensaje = mensaje };
}
