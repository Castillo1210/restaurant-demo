using CaraNegra.Domain.Enums;

namespace CaraNegra.Application.Common.Interfaces;

/// <summary>
/// Consulta el nombre (RENIEC, por DNI) o la razón social (SUNAT, por RUC) en un proveedor
/// externo. Devuelve null si el documento no existe. Lanza ConsultaDocumentoNoDisponibleException
/// si el proveedor no está configurado o no responde (el cajero puede escribir el nombre a mano).
/// </summary>
public interface IConsultaDocumentoService
{
    /// <summary>Nombre del proveedor activo ("ApiPeruDev", "Decolecta", "Simulado"...).</summary>
    string Proveedor { get; }

    Task<ResultadoConsultaDocumento?> ConsultarAsync(TipoDocumentoIdentidad tipo, string numero, CancellationToken cancellationToken = default);
}

public record ResultadoConsultaDocumento
{
    public string Nombre { get; init; } = string.Empty;
    public string? Direccion { get; init; }
    /// <summary>Solo RUC: estado del contribuyente en SUNAT (ACTIVO, BAJA DE OFICIO...).</summary>
    public string? Estado { get; init; }
    /// <summary>Solo RUC: condición del domicilio fiscal (HABIDO, NO HABIDO...).</summary>
    public string? Condicion { get; init; }
}

public class ConsultaDocumentoNoDisponibleException : Exception
{
    public ConsultaDocumentoNoDisponibleException(string mensaje, Exception? inner = null) : base(mensaje, inner) { }
}
