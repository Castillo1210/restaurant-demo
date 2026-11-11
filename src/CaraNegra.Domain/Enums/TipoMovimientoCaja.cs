namespace CaraNegra.Domain.Enums;

/// <summary>Movimiento de efectivo de la caja que no es una venta.</summary>
public enum TipoMovimientoCaja
{
    /// <summary>Entra efectivo (p. ej. sencillo adicional para vuelto).</summary>
    Ingreso,
    /// <summary>Sale efectivo (p. ej. compra de gas, pago a un proveedor).</summary>
    Egreso
}
