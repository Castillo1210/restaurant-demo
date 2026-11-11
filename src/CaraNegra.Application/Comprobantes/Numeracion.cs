using CaraNegra.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Comprobantes;

/// <summary>
/// Numeración de comprobantes: cada serie (B001, F001…) lleva su propio correlativo.
/// El último número es el mayor entre lo ya emitido en este sistema y lo emitido antes con otro
/// sistema (configuración FacturacionElectronica:UltimoNumeroPorSerie). Así, al pasar de otro
/// sistema a este, la serie continúa donde quedó y SUNAT no recibe números repetidos.
/// </summary>
public static class Numeracion
{
    public static async Task<int> UltimoAsync(IApplicationDbContext context, IFacturacionElectronicaService facturacion,
        string serie, CancellationToken cancellationToken)
    {
        var enEsteSistema = await context.Comprobantes
            .Where(c => c.Serie == serie)
            .MaxAsync(c => (int?)c.Correlativo, cancellationToken) ?? 0;

        return Math.Max(enEsteSistema, facturacion.UltimoNumeroAnterior(serie));
    }
}
