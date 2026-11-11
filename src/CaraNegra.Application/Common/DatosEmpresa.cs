namespace CaraNegra.Application.Common;

/// <summary>
/// Datos del emisor (el local) que se imprimen en comprobantes, recibos y comanda.
/// Se leen de la tabla ConfiguracionEmpresa (Admin → Datos de la empresa) mediante
/// IDatosEmpresaProvider. La sección "Empresa" de appsettings.json solo se usa como valor
/// inicial la primera vez que arranca el sistema (EmpresaSeeder).
/// </summary>
public class DatosEmpresa
{
    public string Ruc { get; set; } = "20601234565";
    public string RazonSocial { get; set; } = "RESTAURANTE DEMO S.A.C.";
    public string NombreComercial { get; set; } = "RESTAURANTE DEMO";
    public string Direccion { get; set; } = "Av. Principal 123 - Lima";
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? MensajePie { get; set; }
    public string? LogoBase64 { get; set; }
    public string SerieBoleta { get; set; } = "B001";
    public string SerieFactura { get; set; } = "F001";
    public decimal IgvPorcentaje { get; set; } = 18m;

    /// <summary>
    /// Siempre true en esta versión: no existe integración real con SUNAT/OSE.
    /// Se expone al frontend para mostrar la leyenda "DEMO – SIN VALIDEZ TRIBUTARIA".
    /// </summary>
    public bool ModoSimulacion { get; set; } = true;
}
