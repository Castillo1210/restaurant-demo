using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CaraNegra.API.ConsultaDocumentos;

/// <summary>
/// Consulta de DNI (RENIEC) y RUC (SUNAT) con la API de Decolecta:
///   GET https://api.decolecta.com/v1/reniec/dni?numero=46027897
///   GET https://api.decolecta.com/v1/sunat/ruc?numero=20601030013
///   Header: Authorization: Bearer {token}
///
/// Decolecta cobra por consulta, por eso ConsultarDocumentoCommand busca primero en la base local
/// de clientes y solo llama aquí cuando el cliente es nuevo. Si Decolecta falla se lanza
/// ConsultaDocumentoNoDisponibleException y el cajero escribe el nombre a mano (nunca bloquea la venta).
///
/// Con Proveedor = "Simulado" no se sale a internet: solo se reconocen los documentos de ejemplo
/// de la demo (útil para presentar el sistema sin token).
/// </summary>
public class ConsultaDocumentoService : IConsultaDocumentoService
{
    public const string HttpClientName = "ConsultaDocumentos";
    private const string BaseUrlDecolecta = "https://api.decolecta.com/v1";

    private static readonly Dictionary<string, ResultadoConsultaDocumento> DatosSimulados = new()
    {
        ["45678912"] = new() { Nombre = "PEREZ ROJAS JUAN" },
        ["20555666773"] = new() { Nombre = "CLIENTE EMPRESA DEMO S.A.C.", Direccion = "JR. LOS OLIVOS 456 - LIMA - LIMA - LIMA", Estado = "ACTIVO", Condicion = "HABIDO" },
    };

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IHttpClientFactory _httpFactory;
    private readonly ConsultaDocumentosOptions _options;
    private readonly ILogger<ConsultaDocumentoService> _logger;

    public ConsultaDocumentoService(IHttpClientFactory httpFactory, IOptions<ConsultaDocumentosOptions> options,
        ILogger<ConsultaDocumentoService> logger)
    {
        _httpFactory = httpFactory;
        _options = options.Value;
        _logger = logger;
    }

    public string Proveedor => EsSimulado ? "Simulado" : "Decolecta";

    private bool EsSimulado => string.IsNullOrWhiteSpace(_options.Proveedor)
        || _options.Proveedor.Equals("Simulado", StringComparison.OrdinalIgnoreCase);

    public async Task<ResultadoConsultaDocumento?> ConsultarAsync(TipoDocumentoIdentidad tipo, string numero, CancellationToken cancellationToken = default)
    {
        if (tipo is not (TipoDocumentoIdentidad.DNI or TipoDocumentoIdentidad.RUC))
            return null;

        if (EsSimulado)
            return DatosSimulados.TryGetValue(numero, out var simulado) ? simulado : null;

        if (string.IsNullOrWhiteSpace(_options.Token))
            throw new ConsultaDocumentoNoDisponibleException(
                "La consulta de DNI/RUC no está configurada (falta el token de Decolecta). Ingrese el nombre manualmente.");

        var baseUrl = (string.IsNullOrWhiteSpace(_options.BaseUrl) ? BaseUrlDecolecta : _options.BaseUrl).TrimEnd('/');
        var url = tipo == TipoDocumentoIdentidad.RUC
            ? $"{baseUrl}/sunat/ruc?numero={Uri.EscapeDataString(numero)}"
            : $"{baseUrl}/reniec/dni?numero={Uri.EscapeDataString(numero)}";

        try
        {
            var http = _httpFactory.CreateClient(HttpClientName);
            http.Timeout = TimeSpan.FromSeconds(Math.Max(2, _options.TimeoutSegundos));

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var respuesta = await http.SendAsync(request, cancellationToken);

            // Documento inexistente / número rechazado por el proveedor → "no encontrado".
            if (respuesta.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity)
                return null;

            if (respuesta.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new ConsultaDocumentoNoDisponibleException("El token de Decolecta no es válido o no tiene permiso para esta consulta.");

            if (respuesta.StatusCode == HttpStatusCode.TooManyRequests)
                throw new ConsultaDocumentoNoDisponibleException("Se agotaron las consultas del plan de Decolecta. Ingrese el nombre manualmente.");

            if (!respuesta.IsSuccessStatusCode)
                throw new ConsultaDocumentoNoDisponibleException($"Decolecta respondió {(int)respuesta.StatusCode}. Ingrese el nombre manualmente.");

            return tipo == TipoDocumentoIdentidad.RUC
                ? MapearRuc(await respuesta.Content.ReadFromJsonAsync<DecolectaRuc>(JsonOptions, cancellationToken))
                : MapearDni(await respuesta.Content.ReadFromJsonAsync<DecolectaDni>(JsonOptions, cancellationToken));
        }
        catch (ConsultaDocumentoNoDisponibleException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
            _logger.LogWarning(ex, "Falló la consulta de {Tipo} en Decolecta", tipo);
            throw new ConsultaDocumentoNoDisponibleException("El servicio de consulta de DNI/RUC no respondió. Ingrese el nombre manualmente.", ex);
        }
    }

    // ---------------- Mapeo de respuestas de Decolecta ----------------

    /// <summary>
    /// DNI: { "first_name": "ROXANA KARINA", "first_last_name": "DELGADO", "second_last_name": "HUAMANI",
    ///        "full_name": "DELGADO HUAMANI ROXANA KARINA", "document_number": "46027897" }
    /// </summary>
    internal static ResultadoConsultaDocumento? MapearDni(DecolectaDni? r)
    {
        if (r is null) return null;

        var nombre = Limpio(r.FullName)
            ?? Limpio(string.Join(' ', new[] { r.FirstLastName, r.SecondLastName, r.FirstName }
                .Select(Limpio).Where(p => p is not null)));

        return nombre is null ? null : new ResultadoConsultaDocumento { Nombre = nombre };
    }

    /// <summary>
    /// RUC: { "razon_social": "REXTIE S.A.C.", "numero_documento": "20601030013", "estado": "ACTIVO",
    ///        "condicion": "HABIDO", "direccion": "AV. JOSE GALVEZ BARRENECHEA NRO 566 INT. 101 URB. CORPAC ",
    ///        "distrito": "SAN ISIDRO", "provincia": "LIMA", "departamento": "LIMA", ... }
    /// La dirección fiscal se arma como en los comprobantes: "via ... - DISTRITO - PROVINCIA - DEPARTAMENTO".
    /// </summary>
    internal static ResultadoConsultaDocumento? MapearRuc(DecolectaRuc? r)
    {
        var razonSocial = Limpio(r?.RazonSocial);
        if (r is null || razonSocial is null) return null;

        var partesDireccion = new[] { r.Direccion, r.Distrito, r.Provincia, r.Departamento }
            .Select(Limpio)
            .Where(p => p is not null)
            .ToList();

        return new ResultadoConsultaDocumento
        {
            Nombre = razonSocial,
            Direccion = partesDireccion.Count > 0 ? string.Join(" - ", partesDireccion) : null,
            Estado = Limpio(r.Estado),
            Condicion = Limpio(r.Condicion)
        };
    }

    /// <summary>Normaliza espacios y trata "-" o vacío como "sin dato" (así llega en Decolecta).</summary>
    private static string? Limpio(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var t = string.Join(' ', texto.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return t == "-" ? null : t;
    }

    internal sealed class DecolectaDni
    {
        [JsonPropertyName("first_name")] public string? FirstName { get; set; }
        [JsonPropertyName("first_last_name")] public string? FirstLastName { get; set; }
        [JsonPropertyName("second_last_name")] public string? SecondLastName { get; set; }
        [JsonPropertyName("full_name")] public string? FullName { get; set; }
        [JsonPropertyName("document_number")] public string? DocumentNumber { get; set; }
    }

    internal sealed class DecolectaRuc
    {
        [JsonPropertyName("razon_social")] public string? RazonSocial { get; set; }
        [JsonPropertyName("numero_documento")] public string? NumeroDocumento { get; set; }
        [JsonPropertyName("estado")] public string? Estado { get; set; }
        [JsonPropertyName("condicion")] public string? Condicion { get; set; }
        [JsonPropertyName("direccion")] public string? Direccion { get; set; }
        [JsonPropertyName("distrito")] public string? Distrito { get; set; }
        [JsonPropertyName("provincia")] public string? Provincia { get; set; }
        [JsonPropertyName("departamento")] public string? Departamento { get; set; }
    }
}
