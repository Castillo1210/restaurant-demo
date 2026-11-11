using CaraNegra.Application.Common;
using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Domain.Entities;
using CaraNegra.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Jornadas;

/// <summary>Reglas comunes de la jornada (día operativo).</summary>
public static class JornadaReglas
{
    public const string MensajeSinJornada =
        "El día aún no se ha abierto: el administrador o el cajero debe abrir la jornada antes de tomar pedidos o cobrar.";

    /// <summary>Jornada abierta (la más reciente si, por error, hubiera más de una) o null.</summary>
    public static Task<Jornada?> ObtenerAbiertaAsync(IApplicationDbContext context, CancellationToken ct) =>
        context.Jornadas
            .Where(j => j.Estado == EstadoJornada.Abierta)
            .OrderByDescending(j => j.JornadaId)
            .FirstOrDefaultAsync(ct);

    /// <summary>Lanza InvalidOperationException (400 en la API) si no hay jornada abierta.</summary>
    public static async Task<Jornada> RequerirAbiertaAsync(IApplicationDbContext context, CancellationToken ct) =>
        await ObtenerAbiertaAsync(context, ct) ?? throw new InvalidOperationException(MensajeSinJornada);

    /// <summary>El efectivo se identifica por el nombre del medio de pago ("Efectivo").</summary>
    public static bool EsEfectivo(string? nombreMetodo) =>
        (nombreMetodo ?? string.Empty).Contains("efectivo", StringComparison.OrdinalIgnoreCase);

    /// <summary>Fecha de hoy en Lima (sin hora).</summary>
    public static DateTime HoyLima() => PeruDateRangeHelper.ToHoraPeru(DateTime.UtcNow).Date;
}
