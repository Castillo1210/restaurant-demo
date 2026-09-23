namespace CaraNegra.Domain.Enums;

/// <summary>
/// Tipo de documento de identidad del cliente (catálogo 06 de SUNAT):
/// SinDocumento = "0", DNI = "1", CarnetExtranjeria = "4", RUC = "6".
/// </summary>
public enum TipoDocumentoIdentidad
{
    SinDocumento,
    DNI,
    CarnetExtranjeria,
    RUC
}
