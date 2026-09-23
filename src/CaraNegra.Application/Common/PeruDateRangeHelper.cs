namespace CaraNegra.Application.Common;

/// <summary>
/// Cara Negra opera en una sola ubicación en Perú (UTC-5, sin horario de verano), mientras que
/// todas las fechas se guardan en UTC (<c>CreadoEn = DateTime.UtcNow</c>, ver BaseEntity /
/// ApplicationDbContext.SaveChangesAsync). Los filtros de "rango de fechas" (Reportes, listado
/// de pedidos, listado de pagos) reciben del frontend fechas que representan el calendario
/// LOCAL del usuario (ej.: "hoy" en Lima), así que hay que convertir esos límites de día
/// calendario-Lima a instantes UTC antes de compararlos contra CreadoEn.
///
/// Sin esta conversión, cualquier pedido hecho pasadas las ~7pm hora de Lima ya cae en el día
/// UTC siguiente (Lima + 5h cruza la medianoche UTC) y desaparece de "hoy" hasta que se pone
/// "mañana" como fechaHasta — este era exactamente el bug reportado: "los pedidos que se
/// pidieron recién me sale cuando en fechaHasta pongo el día siguiente".
/// </summary>
public static class PeruDateRangeHelper
{
    private static readonly TimeSpan OffsetLima = TimeSpan.FromHours(-5);

    /// <summary>Instante UTC correspondiente al inicio (00:00:00) de una fecha calendario de Lima.</summary>
    public static DateTime InicioDelDiaUtc(DateTime fechaLocal) => fechaLocal.Date - OffsetLima;

    /// <summary>Instante UTC correspondiente al final (23:59:59.9999999) de una fecha calendario de Lima.</summary>
    public static DateTime FinDelDiaUtc(DateTime fechaLocal) => fechaLocal.Date.AddDays(1) - OffsetLima - TimeSpan.FromTicks(1);

    /// <summary>Convierte un instante UTC a la hora local de Lima (para imprimir fechas en comprobantes).</summary>
    public static DateTime ToHoraPeru(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Unspecified) + OffsetLima;
}
