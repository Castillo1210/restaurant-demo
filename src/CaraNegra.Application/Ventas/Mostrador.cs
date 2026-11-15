using CaraNegra.Domain.Entities;
using CaraNegra.Domain.Enums;

namespace CaraNegra.Application.Ventas;

/// <summary>
/// Reglas de la venta de mostrador (piscina): productos que no van a cocina, la mesa interna de
/// mostrador y el rol TAQUILLA, que trabaja con su propia caja.
/// </summary>
public static class Mostrador
{
    /// <summary>Persona de la boletería de la piscina: solo ve "Venta rápida" y su caja.</summary>
    public const string RolTaquilla = "TAQUILLA";

    /// <summary>Número de la mesa interna (no se muestra en el mapa de mesas).</summary>
    public const string NumeroMesa = "MOSTRADOR";

    /// <summary>El producto es de una categoría de mostrador (requiere Producto.Categoria cargada).</summary>
    public static bool EsDeMostrador(Producto? producto) => producto?.Categoria?.EsMostrador ?? false;

    /// <summary>La línea del pedido sale en la comanda / pantalla de cocina.</summary>
    public static bool VaACocina(DetallePedido detalle) => !EsDeMostrador(detalle.Producto);

    /// <summary>Caja con la que trabaja un usuario: la taquilla, la de la piscina; el resto, la del restaurante.</summary>
    public static CajaJornada CajaDelRol(bool esTaquilla) => esTaquilla ? CajaJornada.Piscina : CajaJornada.Restaurante;

    public static string NombreCaja(CajaJornada caja) => caja == CajaJornada.Piscina ? "Piscina" : "Restaurante";
}
