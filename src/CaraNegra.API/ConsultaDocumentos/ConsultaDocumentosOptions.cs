namespace CaraNegra.API.ConsultaDocumentos;

/// <summary>
/// Configuración de la consulta de DNI (RENIEC) / RUC (SUNAT). Sección "ConsultaDocumentos"
/// de appsettings.json. El Token NUNCA va en appsettings.json: configúrelo con
///   dotnet user-secrets set "ConsultaDocumentos:Token" "SU_TOKEN"
/// o la variable de entorno ConsultaDocumentos__Token.
/// </summary>
public class ConsultaDocumentosOptions
{
    /// <summary>"Decolecta" (api.decolecta.com, DNI y RUC) o "Simulado" (sin internet, solo documentos de ejemplo).</summary>
    public string Proveedor { get; set; } = "Decolecta";

    /// <summary>Opcional: sobrescribe la URL base (por defecto https://api.decolecta.com/v1).</summary>
    public string? BaseUrl { get; set; }

    public string? Token { get; set; }

    public int TimeoutSegundos { get; set; } = 8;
}
