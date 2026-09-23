namespace CaraNegra.Domain.Enums;

/// <summary>
/// Estado del comprobante. En la demo, "AceptadoSimulado" imita la respuesta (CDR) que
/// devolvería SUNAT/OSE; NO hay ningún envío real a SUNAT.
/// </summary>
public enum EstadoComprobante
{
    AceptadoSimulado,
    Anulado
}
