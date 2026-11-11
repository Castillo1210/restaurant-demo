namespace CaraNegra.Application.Empresa.DTOs;

public class ConfiguracionEmpresaDto
{
    public string Ruc { get; set; } = string.Empty;
    public string RazonSocial { get; set; } = string.Empty;
    public string NombreComercial { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string SerieBoleta { get; set; } = string.Empty;
    public string SerieFactura { get; set; } = string.Empty;
    public decimal IgvPorcentaje { get; set; }
    public string? MensajePie { get; set; }
    public string? LogoBase64 { get; set; }
    public bool ModoSimulacion { get; set; }
    public DateTime? ActualizadoEn { get; set; }
}

public class UpdateConfiguracionEmpresaDto
{
    public string Ruc { get; set; } = string.Empty;
    public string RazonSocial { get; set; } = string.Empty;
    public string NombreComercial { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string SerieBoleta { get; set; } = string.Empty;
    public string SerieFactura { get; set; } = string.Empty;
    public decimal IgvPorcentaje { get; set; }
    public string? MensajePie { get; set; }
    /// <summary>data URL de la imagen; null o "" = sin logo.</summary>
    public string? LogoBase64 { get; set; }
}

/// <summary>
/// Datos sin sesión (pantalla de login, título de la app, cabecera de los tickets de caja).
/// RUC, razón social y dirección son públicos: salen impresos en cada boleta.
/// </summary>
public class EmpresaPublicaDto
{
    public string NombreComercial { get; set; } = string.Empty;
    public string Ruc { get; set; } = string.Empty;
    public string RazonSocial { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;
    public string? LogoBase64 { get; set; }
    public string? MensajePie { get; set; }
}
