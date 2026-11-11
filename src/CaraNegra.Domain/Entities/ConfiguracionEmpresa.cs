using CaraNegra.Domain.Common;

namespace CaraNegra.Domain.Entities;

/// <summary>
/// Datos de la empresa (el local) que usa todo el sistema: comprobantes electrónicos, recibos,
/// comanda impresa, pantalla de login, etc. Es una tabla de UNA sola fila (Id = 1) que se edita
/// desde Admin → Datos de la empresa, para poder instalar el sistema en otro restaurante sin
/// tocar código ni appsettings.json.
/// </summary>
public class ConfiguracionEmpresa : BaseEntity
{
    public const int IdUnico = 1;

    public int ConfiguracionEmpresaId { get; set; } = IdUnico;

    public string Ruc { get; set; } = string.Empty;
    public string RazonSocial { get; set; } = string.Empty;
    public string NombreComercial { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string? Email { get; set; }

    // Comprobantes
    public string SerieBoleta { get; set; } = "B001";
    public string SerieFactura { get; set; } = "F001";
    public decimal IgvPorcentaje { get; set; } = 18m;

    /// <summary>Mensaje al pie de boletas/facturas/recibos (p.ej. "¡Gracias por su visita!").</summary>
    public string? MensajePie { get; set; }

    /// <summary>Logo como data URL ("data:image/png;base64,..."), reducido en el navegador antes de subirlo.</summary>
    public string? LogoBase64 { get; set; }
}
