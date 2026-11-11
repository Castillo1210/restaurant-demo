using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CaraNegra.API.FacturacionElectronica;

/// <summary>
/// Envío de comprobantes a APISUNAT (Lucode), un PSE: arma el XML, lo firma y lo envía a SUNAT.
///   Sandbox:    https://sandbox.apisunat.pe  (pruebas, NO llega a SUNAT)
///   Producción: https://app.apisunat.pe
///   POST /api/v3/documents  → emitir
///   POST /api/v3/status     → consultar estado { documento, serie, numero }
///   Header: Authorization: Bearer {token}   (un token por empresa y por ambiente)
///
/// Respuestas: 200 { success, message, payload: { estado: ACEPTADO|PENDIENTE|RECHAZADO, hash, xml,
/// cdr, pdf: { ticket, a4 } } } · 422/400 { success:false, message } · 401 token inválido.
///
/// Con Proveedor = "Simulado" no sale a internet (demo).
/// </summary>
public class FacturacionElectronicaService : IFacturacionElectronicaService
{
    public const string HttpClientName = "FacturacionElectronica";

    private const string UrlSandbox = "https://sandbox.apisunat.pe";
    private const string UrlProduccion = "https://app.apisunat.pe";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IHttpClientFactory _httpFactory;
    private readonly FacturacionElectronicaOptions _options;
    private readonly ILogger<FacturacionElectronicaService> _logger;

    public FacturacionElectronicaService(IHttpClientFactory httpFactory, IOptions<FacturacionElectronicaOptions> options,
        ILogger<FacturacionElectronicaService> logger)
    {
        _httpFactory = httpFactory;
        _options = options.Value;
        _logger = logger;
    }

    public bool EsSimulado => !string.Equals(_options.Proveedor, "ApiSunat", StringComparison.OrdinalIgnoreCase);

    public string Proveedor => EsSimulado ? "Simulado" : "ApiSunat";

    public string Ambiente => EsSimulado
        ? "SIMULADO"
        : string.Equals(_options.Ambiente, "Produccion", StringComparison.OrdinalIgnoreCase)
          || string.Equals(_options.Ambiente, "Producción", StringComparison.OrdinalIgnoreCase)
            ? "PRODUCCION"
            : "SANDBOX";

    private string BaseUrl =>
        (string.IsNullOrWhiteSpace(_options.BaseUrl)
            ? (Ambiente == "PRODUCCION" ? UrlProduccion : UrlSandbox)
            : _options.BaseUrl).TrimEnd('/');

    public int UltimoNumeroAnterior(string serie)
    {
        foreach (var (clave, numero) in _options.UltimoNumeroPorSerie)
            if (string.Equals(clave.Trim(), serie.Trim(), StringComparison.OrdinalIgnoreCase))
                return Math.Max(0, numero);
        return 0;
    }

    public Task<RespuestaFacturacion> EnviarAsync(SolicitudComprobante solicitud, CancellationToken cancellationToken = default)
    {
        ApiSunatDocumento cuerpo;
        try
        {
            cuerpo = ApiSunatPayload.Construir(solicitud);
        }
        catch (InvalidOperationException ex)
        {
            return Task.FromResult(RespuestaFacturacion.De(ResultadoFacturacion.ErrorDatos, ex.Message));
        }

        return PostAsync("/api/v3/documents", cuerpo, esConsulta: false, cancellationToken);
    }

    public Task<RespuestaFacturacion> ConsultarEstadoAsync(TipoComprobante tipo, string serie, int numero,
        CancellationToken cancellationToken = default)
    {
        var cuerpo = new
        {
            documento = tipo == TipoComprobante.Factura ? "factura" : "boleta",
            serie,
            numero
        };
        return PostAsync("/api/v3/status", cuerpo, esConsulta: true, cancellationToken);
    }

    private async Task<RespuestaFacturacion> PostAsync(string ruta, object cuerpo, bool esConsulta, CancellationToken ct)
    {
        if (EsSimulado)
            return RespuestaFacturacion.De(ResultadoFacturacion.ErrorDatos, "La facturación está en modo Simulado.");

        if (string.IsNullOrWhiteSpace(_options.Token))
            return RespuestaFacturacion.De(ResultadoFacturacion.SinConexion,
                "Falta el token de APISUNAT (FacturacionElectronica:Token). Se reintentará cuando esté configurado.");

        try
        {
            var http = _httpFactory.CreateClient(HttpClientName);
            http.Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSegundos, 5, 120));

            using var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl + ruta)
            {
                Content = JsonContent.Create(cuerpo, cuerpo.GetType())
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var respuesta = await http.SendAsync(request, ct);
            var texto = await respuesta.Content.ReadAsStringAsync(ct);
            var r = Interpretar(respuesta.StatusCode, texto, esConsulta);

            if (r.Resultado is not (ResultadoFacturacion.Aceptado or ResultadoFacturacion.Pendiente))
                _logger.LogWarning("APISUNAT {Ruta} → {Status} {Resultado}: {Mensaje}", ruta, (int)respuesta.StatusCode, r.Resultado, r.Mensaje);

            return r;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
            if (ct.IsCancellationRequested) throw;
            _logger.LogWarning(ex, "Sin respuesta de APISUNAT en {Ruta}", ruta);
            return RespuestaFacturacion.De(ResultadoFacturacion.SinConexion,
                "No hubo respuesta de APISUNAT (sin internet o servicio caído). Se reintentará automáticamente.");
        }
    }

    /// <summary>Traduce la respuesta HTTP de APISUNAT a un resultado del sistema.</summary>
    internal static RespuestaFacturacion Interpretar(HttpStatusCode status, string texto, bool esConsulta)
    {
        RespuestaApiSunat? cuerpo = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(texto))
                cuerpo = JsonSerializer.Deserialize<RespuestaApiSunat>(texto, JsonOptions);
        }
        catch (JsonException)
        {
            // Respuesta que no es JSON (p. ej. página de error de un proxy).
        }

        var mensaje = MensajeCompleto(cuerpo);
        var codigo = (int)status;

        if (codigo is 401 or 403)
            return RespuestaFacturacion.De(ResultadoFacturacion.SinConexion,
                $"APISUNAT rechazó el token ({mensaje ?? "acceso no autorizado"}). Revisa FacturacionElectronica:Token y el ambiente.");

        if (codigo >= 500 || codigo == 429 || codigo == 408)
            return RespuestaFacturacion.De(ResultadoFacturacion.SinConexion,
                $"APISUNAT no está disponible ({codigo}). Se reintentará automáticamente.");

        if (status == HttpStatusCode.OK && cuerpo?.Payload is { } p)
        {
            var resultado = (p.Estado ?? string.Empty).Trim().ToUpperInvariant() switch
            {
                "ACEPTADO" => ResultadoFacturacion.Aceptado,
                "PENDIENTE" => ResultadoFacturacion.Pendiente,
                "RECHAZADO" => ResultadoFacturacion.Rechazado,
                // "OBSERVADO" también es aceptado por SUNAT (con observaciones).
                "OBSERVADO" => ResultadoFacturacion.Aceptado,
                _ => ResultadoFacturacion.Pendiente
            };

            return new RespuestaFacturacion
            {
                Resultado = resultado,
                Mensaje = mensaje,
                Hash = p.Hash,
                XmlUrl = p.Xml,
                CdrUrl = p.Cdr,
                PdfUrl = p.Pdf?.Ticket ?? p.Pdf?.A4
            };
        }

        var minusculas = (mensaje ?? string.Empty).ToLowerInvariant();

        if (esConsulta && (minusculas.Contains("no se encuentra") || status == HttpStatusCode.NotFound))
            return RespuestaFacturacion.De(ResultadoFacturacion.NoEncontrado, mensaje);

        if (!esConsulta && (minusculas.Contains("emitido anteriormente") || minusculas.Contains("ya existe")
            || minusculas.Contains("duplicad") || status == HttpStatusCode.Conflict))
            return RespuestaFacturacion.De(ResultadoFacturacion.Duplicado, mensaje);

        if (codigo is 400 or 422)
            return RespuestaFacturacion.De(ResultadoFacturacion.ErrorDatos, mensaje ?? $"APISUNAT respondió {codigo}.");

        return RespuestaFacturacion.De(ResultadoFacturacion.SinConexion, mensaje ?? $"Respuesta inesperada de APISUNAT ({codigo}).");
    }

    /// <summary>message + detalle de validación (payload: { campo: ["error", ...] }) si viene.</summary>
    private static string? MensajeCompleto(RespuestaApiSunat? cuerpo)
    {
        if (cuerpo is null) return null;
        var mensaje = cuerpo.Message?.Trim();

        if (cuerpo.PayloadCrudo is { ValueKind: JsonValueKind.Object } crudo && !crudo.TryGetProperty("estado", out _))
        {
            var detalles = new List<string>();
            foreach (var prop in crudo.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Array)
                    detalles.AddRange(prop.Value.EnumerateArray()
                        .Where(v => v.ValueKind == JsonValueKind.String)
                        .Select(v => v.GetString()!));
                else if (prop.Value.ValueKind == JsonValueKind.String)
                    detalles.Add(prop.Value.GetString()!);
            }
            if (detalles.Count > 0)
                mensaje = string.IsNullOrEmpty(mensaje) ? string.Join(" · ", detalles) : $"{mensaje}: {string.Join(" · ", detalles)}";
        }

        return string.IsNullOrWhiteSpace(mensaje) ? null : mensaje;
    }

    internal sealed class RespuestaApiSunat
    {
        [JsonPropertyName("success")] public bool? Success { get; set; }
        [JsonPropertyName("message")] public string? Message { get; set; }

        /// <summary>El payload puede ser el estado del comprobante o un detalle de errores de validación.</summary>
        [JsonPropertyName("payload")] public JsonElement? PayloadCrudo { get; set; }

        [JsonIgnore]
        public PayloadApiSunat? Payload =>
            PayloadCrudo is { ValueKind: JsonValueKind.Object } p && p.TryGetProperty("estado", out _)
                ? p.Deserialize<PayloadApiSunat>(JsonOptions)
                : null;
    }

    internal sealed class PayloadApiSunat
    {
        [JsonPropertyName("estado")] public string? Estado { get; set; }
        [JsonPropertyName("hash")] public string? Hash { get; set; }
        [JsonPropertyName("xml")] public string? Xml { get; set; }
        [JsonPropertyName("cdr")] public string? Cdr { get; set; }
        [JsonPropertyName("pdf")] public PdfApiSunat? Pdf { get; set; }
    }

    internal sealed class PdfApiSunat
    {
        [JsonPropertyName("ticket")] public string? Ticket { get; set; }
        [JsonPropertyName("a4")] public string? A4 { get; set; }
    }
}
