using CaraNegra.Domain.Common;
using CaraNegra.Domain.Enums;

namespace CaraNegra.Domain.Entities;

/// <summary>
/// Entrada o salida de efectivo de la caja que no es una venta (compra de gas, pago a un
/// proveedor, sencillo adicional…). Sin registrarlos, el efectivo nunca cuadraría al cierre.
/// Solo se pueden anular (no borrar) mientras la jornada sigue abierta.
/// </summary>
public class MovimientoCaja : BaseEntity
{
    public int MovimientoCajaId { get; set; }
    public int JornadaId { get; set; }

    public TipoMovimientoCaja Tipo { get; set; }
    public decimal Monto { get; set; }
    public string Motivo { get; set; } = string.Empty;

    public int UsuarioId { get; set; }
    public string RegistradoPor { get; set; } = string.Empty;

    public bool EstaAnulado { get; set; }
    public DateTime? AnuladoEn { get; set; }
    public string? AnuladoPor { get; set; }

    public Jornada Jornada { get; set; } = null!;
}
