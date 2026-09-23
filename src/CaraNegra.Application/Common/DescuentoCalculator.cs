using CaraNegra.Domain.Entities;

namespace CaraNegra.Application.Common;

/// <summary>
/// Calcula el monto de descuento y el nuevo Total de un pedido a partir de su SubTotal y el
/// descuento activo (si tiene uno aplicado). Se recalcula siempre desde cero (no se guarda un
/// monto de descuento "congelado") porque el SubTotal puede seguir cambiando después de aplicar
/// el descuento (el mozo agrega o quita ítems), así que un porcentaje debe recalcularse sobre
/// el SubTotal vigente en cada momento.
/// </summary>
public static class DescuentoCalculator
{
    public static decimal CalcularMonto(decimal subTotal, Descuento? descuento)
    {
        if (descuento == null) return 0m;

        var monto = descuento.EsPorcentaje ? subTotal * (descuento.Valor / 100m) : descuento.Valor;

        // Nunca negativo ni mayor al propio subtotal (evita un Total negativo).
        if (monto < 0m) monto = 0m;
        if (monto > subTotal) monto = subTotal;

        return monto;
    }

    public static decimal CalcularTotal(decimal subTotal, Descuento? descuento) =>
        subTotal - CalcularMonto(subTotal, descuento);
}
