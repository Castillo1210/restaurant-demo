using CaraNegra.Domain.Common;
using CaraNegra.Domain.Enums;

namespace CaraNegra.Domain.Entities;

/// <summary>
/// Jornada = día operativo del restaurante. Se abre al empezar (con el fondo de caja) y se cierra
/// con el arqueo. NO depende del día calendario: si se atiende pasada la medianoche, esas ventas
/// siguen siendo de la jornada abierta. Solo puede haber una jornada abierta a la vez.
///
/// Cada pedido y cada pago quedan vinculados a la jornada en la que se registraron. Al cerrar se
/// guarda una "foto" de los totales y el arqueo, y la jornada queda de solo lectura.
/// </summary>
public class Jornada : BaseEntity
{
    public int JornadaId { get; set; }

    /// <summary>Día al que corresponde (solo fecha, hora de Lima).</summary>
    public DateTime FechaOperativa { get; set; }

    public EstadoJornada Estado { get; set; } = EstadoJornada.Abierta;

    // Apertura
    public DateTime AbiertaEn { get; set; } = DateTime.UtcNow;
    public int AbiertaPorUsuarioId { get; set; }
    public string AbiertaPor { get; set; } = string.Empty;
    /// <summary>Efectivo con el que empieza la caja (para dar vuelto).</summary>
    public decimal FondoInicial { get; set; }
    public string? ObservacionApertura { get; set; }

    // Cierre
    public DateTime? CerradaEn { get; set; }
    public int? CerradaPorUsuarioId { get; set; }
    public string? CerradaPor { get; set; }
    public string? ObservacionCierre { get; set; }

    // Foto de totales al cierre
    public decimal TotalVentas { get; set; }
    public int CantidadPedidos { get; set; }
    public decimal TotalIngresos { get; set; }
    public decimal TotalEgresos { get; set; }
    public decimal EfectivoEsperado { get; set; }
    public decimal EfectivoContado { get; set; }
    /// <summary>Suma de diferencias del arqueo (contado − esperado). Negativo = faltante.</summary>
    public decimal DiferenciaTotal { get; set; }

    public ICollection<JornadaArqueo> Arqueos { get; set; } = new List<JornadaArqueo>();
    public ICollection<MovimientoCaja> Movimientos { get; set; } = new List<MovimientoCaja>();
}
