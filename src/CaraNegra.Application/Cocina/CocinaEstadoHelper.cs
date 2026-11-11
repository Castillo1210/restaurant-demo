using CaraNegra.Domain.Entities;
using CaraNegra.Domain.Enums;

namespace CaraNegra.Application.Cocina;

/// <summary>
/// Regla única para derivar el estado del PEDIDO a partir del estado de sus ÍTEMS:
///  - todos los ítems activos Listos        → pedido Listo
///  - alguno en preparación o ya listo      → pedido En preparación
///  - ninguno empezado                      → se mantiene Pendiente
/// Solo aplica a pedidos que todavía están en cocina (Pendiente / EnPreparacion / Listo):
/// nunca toca pedidos Entregados (pagados) ni Cancelados.
/// </summary>
public static class CocinaEstadoHelper
{
    public static EstadoPedido CalcularEstadoPedido(Pedido pedido)
    {
        if (pedido.EstadoPedido is EstadoPedido.Entregado or EstadoPedido.Cancelado)
            return pedido.EstadoPedido;

        var activos = pedido.DetallesPedidos
            .Where(d => d.EstadoDetallePedido is not (EstadoDetallePedido.Cancelado or EstadoDetallePedido.Entregado))
            .ToList();

        if (activos.Count == 0)
            return pedido.EstadoPedido;

        if (activos.All(d => d.EstadoDetallePedido == EstadoDetallePedido.Listo))
            return EstadoPedido.Listo;

        if (activos.Any(d => d.EstadoDetallePedido is EstadoDetallePedido.EnPreparacion or EstadoDetallePedido.Listo))
            return EstadoPedido.EnPreparacion;

        // Nada empezado: si el pedido ya estaba en preparación (p.ej. se agregó un ítem nuevo
        // y se deshizo el otro) se deja como estaba para no "retroceder" visualmente.
        return pedido.EstadoPedido == EstadoPedido.Listo ? EstadoPedido.EnPreparacion : pedido.EstadoPedido;
    }
}
