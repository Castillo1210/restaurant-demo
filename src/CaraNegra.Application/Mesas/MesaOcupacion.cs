using System.Linq.Expressions;
using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Domain.Entities;
using CaraNegra.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Mesas;

/// <summary>
/// Regla ÚNICA de ocupación de mesas: cuándo una mesa queda libre y cuándo vuelve a ocuparse.
/// Todos los comandos (cobrar, anular pedido, cambiar de mesa, anular un pago…) pasan por aquí
/// para que la regla no se repita ni se contradiga entre ellos.
///
/// Una mesa está ocupada mientras tenga al menos un pedido ABIERTO: ni cobrado (Entregado)
/// ni anulado (Cancelado).
/// </summary>
public static class MesaOcupacion
{
    /// <summary>Pedido abierto = ni Entregado (cobrado) ni Cancelado (anulado). Traducible a SQL.</summary>
    public static readonly Expression<Func<Pedido, bool>> PedidoAbierto =
        p => p.EstadoPedido != EstadoPedido.Cancelado && p.EstadoPedido != EstadoPedido.Entregado;

    /// <summary>
    /// ¿Le queda a la mesa algún pedido abierto aparte de <paramref name="pedidoExcluidoId"/>?
    /// </summary>
    public static Task<bool> TieneOtrosPedidosAbiertosAsync(IApplicationDbContext context, int mesaId,
        int pedidoExcluidoId, CancellationToken cancellationToken)
        => context.Pedidos
            .Where(PedidoAbierto)
            .AnyAsync(p => p.MesaId == mesaId && p.PedidoId != pedidoExcluidoId, cancellationToken);

    /// <summary>
    /// Deja la mesa Disponible si no le queda otro pedido abierto. Devuelve true si cambió su estado
    /// (para avisar a las pantallas). No guarda: quien llama hace SaveChanges.
    ///
    /// <paramref name="pedidoQueSaleId"/> (el pedido que se cobra, anula o cambia de mesa) se excluye
    /// a propósito: su nuevo estado todavía no está guardado en la base, y si no se excluyera la
    /// consulta lo vería con su estado viejo (abierto) y la mesa nunca se liberaría.
    /// </summary>
    public static async Task<bool> LiberarSiQuedaLibreAsync(IApplicationDbContext context, Mesa? mesa,
        int pedidoQueSaleId, CancellationToken cancellationToken)
    {
        if (mesa is null || mesa.Estado == EstadoMesa.Disponible)
            return false;

        if (await TieneOtrosPedidosAbiertosAsync(context, mesa.MesaId, pedidoQueSaleId, cancellationToken))
            return false;

        mesa.Estado = EstadoMesa.Disponible;
        return true;
    }

    /// <summary>Igual que la otra sobrecarga, buscando la mesa por id.</summary>
    public static async Task<bool> LiberarSiQuedaLibreAsync(IApplicationDbContext context, int mesaId,
        int pedidoQueSaleId, CancellationToken cancellationToken)
    {
        var mesa = await context.Mesas.FindAsync(new object[] { mesaId }, cancellationToken);
        return await LiberarSiQuedaLibreAsync(context, mesa, pedidoQueSaleId, cancellationToken);
    }

    /// <summary>
    /// Estado que debe quedar al EDITAR una mesa desde Administrar. El formulario manda el estado
    /// que tenía al abrirse, así que no se le puede creer a ciegas:
    ///   · con pedido abierto la mesa sigue Ocupada (y no se puede poner en Mantenimiento);
    ///   · sin pedido abierto no puede quedar Ocupada (queda Disponible).
    /// </summary>
    public static async Task<EstadoMesa> EstadoAlEditarAsync(IApplicationDbContext context, Mesa mesa,
        EstadoMesa solicitado, CancellationToken cancellationToken)
    {
        var tienePedido = await TieneOtrosPedidosAbiertosAsync(context, mesa.MesaId, pedidoExcluidoId: 0, cancellationToken);
        if (tienePedido)
        {
            if (solicitado == EstadoMesa.Mantenimiento)
                throw new InvalidOperationException(
                    $"La mesa {mesa.NumeroMesa} tiene un pedido abierto: cóbralo o anúlalo antes de ponerla en mantenimiento.");
            return EstadoMesa.Ocupada;
        }

        return solicitado == EstadoMesa.Ocupada ? EstadoMesa.Disponible : solicitado;
    }

    /// <summary>
    /// Marca la mesa como Ocupada (pedido nuevo, cambio de mesa, o un pago anulado que reabre la cuenta).
    /// Devuelve true si cambió su estado. No guarda: quien llama hace SaveChanges.
    /// </summary>
    public static bool Ocupar(Mesa? mesa)
    {
        if (mesa is null || mesa.Estado == EstadoMesa.Ocupada)
            return false;

        mesa.Estado = EstadoMesa.Ocupada;
        return true;
    }
}
