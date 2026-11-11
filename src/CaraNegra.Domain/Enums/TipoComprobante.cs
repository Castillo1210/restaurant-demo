namespace CaraNegra.Domain.Enums;

/// <summary>
/// Tipo de comprobante de pago electrónico (catálogo 01 de SUNAT).
/// Boleta = "03" (consumidor final), Factura = "01" (empresa con RUC).
/// </summary>
public enum TipoComprobante
{
    Boleta,
    Factura
}
