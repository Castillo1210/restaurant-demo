namespace CaraNegra.Domain.Enums;

/// <summary>
/// En qué va la anulación de un comprobante ante SUNAT (comunicación de baja, resumen diario o
/// nota de crédito). Mientras no esté Aceptada, el comprobante sigue vigente.
/// </summary>
public enum EstadoAnulacion
{
    /// <summary>Aún no llegó al proveedor (sin internet, caída). Se reintenta solo.</summary>
    PorEnviar,
    /// <summary>El proveedor la recibió; falta que SUNAT la confirme. Se consulta sola.</summary>
    Pendiente,
    /// <summary>SUNAT la aceptó: el comprobante quedó anulado.</summary>
    Aceptada,
    /// <summary>El proveedor o SUNAT no la aceptó (el motivo queda en AnulacionMensaje).</summary>
    Rechazada
}
