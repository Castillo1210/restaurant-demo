namespace CaraNegra.API.FacturacionElectronica;

/// <summary>
/// Sección "FacturacionElectronica" de appsettings.json.
///
///   Proveedor: "Simulado" (demo, sin envío) o "ApiSunat" (APISUNAT / Lucode, PSE).
///   Ambiente:  "Sandbox" (pruebas, sin validez) o "Produccion" (envío real a SUNAT).
///
/// El Token NUNCA va en appsettings.json. Configúrelo con:
///   dotnet user-secrets set "FacturacionElectronica:Token" "SU_TOKEN"
/// (el token de sandbox y el de producción son distintos; se obtienen en app.apisunat.pe →
/// Organizaciones).
/// </summary>
public class FacturacionElectronicaOptions
{
    public string Proveedor { get; set; } = "Simulado";

    public string Ambiente { get; set; } = "Sandbox";

    /// <summary>Opcional: sobrescribe la URL base del ambiente.</summary>
    public string? BaseUrl { get; set; }

    public string? Token { get; set; }

    public int TimeoutSegundos { get; set; } = 20;

    /// <summary>Cada cuánto se reintentan los comprobantes PorEnviar / Pendiente (0 = nunca).</summary>
    public int IntervaloReintentoSegundos { get; set; } = 120;

    /// <summary>
    /// Último número YA EMITIDO en cada serie antes de usar este sistema (con otro sistema de
    /// facturación), para continuar la numeración sin repetir números ante SUNAT. Ejemplo:
    ///   "UltimoNumeroPorSerie": { "BE01": 12198, "FE01": 1503 }
    /// → la próxima boleta sale BE01-00012199 y la próxima factura FE01-00001504.
    /// Si en la base ya hay números mayores en esa serie, se sigue desde la base.
    /// </summary>
    public Dictionary<string, int> UltimoNumeroPorSerie { get; set; } = new();
}
