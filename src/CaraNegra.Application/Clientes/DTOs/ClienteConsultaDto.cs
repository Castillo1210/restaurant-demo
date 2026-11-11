using CaraNegra.Domain.Enums;

namespace CaraNegra.Application.Clientes.DTOs;

public class ClienteConsultaDto
{
    public TipoDocumentoIdentidad TipoDocumento { get; set; }
    public string NumeroDocumento { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Dirección fiscal (RUC). Se guarda en la base de clientes.</summary>
    public string? Direccion { get; set; }

    /// <summary>Estado / condición del contribuyente en SUNAT: solo cuando viene de la API.</summary>
    public string? Estado { get; set; }
    public string? Condicion { get; set; }

    /// <summary>"Clientes" si se encontró en la base local; si no, el proveedor de la API.</summary>
    public string Fuente { get; set; } = string.Empty;
    public bool DesdeBaseLocal { get; set; }
}
