namespace CaraNegra.Application.Common;

/// <summary>
/// Datos del emisor (el local) que se imprimen en los comprobantes electrónicos.
/// Se cargan desde la sección "Empresa" de appsettings.json (ver Program.cs).
/// </summary>
public class DatosEmpresa
{
    public string Ruc { get; set; } = "20601234565";
    public string RazonSocial { get; set; } = "RESTAURANTE DEMO S.A.C.";
    public string NombreComercial { get; set; } = "RESTAURANTE DEMO";
    public string Direccion { get; set; } = "Av. Principal 123 - Lima";
    public string? Telefono { get; set; }
    public string SerieBoleta { get; set; } = "B001";
    public string SerieFactura { get; set; } = "F001";
    public decimal IgvPorcentaje { get; set; } = 18m;

    /// <summary>
    /// Siempre true en esta versión: no existe integración real con SUNAT/OSE.
    /// Se expone al frontend para mostrar la leyenda "DEMO – SIN VALIDEZ TRIBUTARIA".
    /// </summary>
    public bool ModoSimulacion { get; set; } = true;
}
