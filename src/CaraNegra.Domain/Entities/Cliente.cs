using CaraNegra.Domain.Common;
using CaraNegra.Domain.Enums;

namespace CaraNegra.Domain.Entities;

/// <summary>
/// Cliente identificado por documento (DNI, C.E. o RUC). Funciona como caché local de la API de
/// consulta de documentos: la primera vez que se consulta un DNI/RUC se llama a la API externa
/// (que cobra por consulta) y el resultado se guarda aquí; las siguientes compras del mismo
/// cliente se resuelven desde esta tabla, sin volver a llamar a la API.
/// Se guarda lo mínimo: tipo y número de documento, nombre o razón social y, en los RUC, la
/// dirección fiscal (va impresa en la factura).
/// </summary>
public class Cliente : BaseEntity
{
    public int ClienteId { get; set; }
    public TipoDocumentoIdentidad TipoDocumento { get; set; }
    public string NumeroDocumento { get; set; } = string.Empty;

    /// <summary>Nombre completo (DNI / C.E.) o razón social (RUC).</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Dirección fiscal (RUC, de SUNAT o corregida por el cajero). Null si no se conoce.</summary>
    public string? Direccion { get; set; }
}
