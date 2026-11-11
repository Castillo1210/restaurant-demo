namespace CaraNegra.Domain.Enums;

/// <summary>
/// Estado del comprobante frente a SUNAT.
///
/// Con el proveedor "Simulado" (demo) todo queda en AceptadoSimulado, sin envío real.
/// Con un proveedor real (APISUNAT, en sandbox o producción) el comprobante pasa por:
///   PorEnviar  → aún no se pudo entregar al proveedor (sin internet, caída, token…); se reintenta solo.
///   Pendiente  → el proveedor lo firmó y lo envió, pero SUNAT aún no responde; se consulta solo.
///   Aceptado   → SUNAT lo aceptó (hay CDR).
///   Rechazado  → SUNAT lo rechazó por datos incorrectos: hay que emitir uno nuevo.
///   ErrorEnvio → el proveedor no aceptó los datos (validación); no llegó a SUNAT.
/// </summary>
public enum EstadoComprobante
{
    AceptadoSimulado,
    Anulado,
    PorEnviar,
    Pendiente,
    Aceptado,
    Rechazado,
    ErrorEnvio
}
