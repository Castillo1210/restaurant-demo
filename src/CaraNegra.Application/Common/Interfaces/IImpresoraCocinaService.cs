namespace CaraNegra.Application.Common.Interfaces;

/// <summary>
/// Envía la comanda de un pedido a la impresora térmica de cocina conectada a la red local
/// (Fase 6). La implementación NUNCA debe lanzar una excepción: si la impresora está apagada,
/// desconectada, o no está configurada, debe registrar el problema (log) y retornar
/// normalmente — imprimir la comanda es una comodidad operativa, no debe poder bloquear ni
/// hacer fallar la toma de pedidos.
/// </summary>
public interface IImpresoraCocinaService
{
    Task ImprimirComandaAsync(ComandaCocina comanda, CancellationToken cancellationToken = default);

    /// <summary>
    /// Devuelve el texto exacto que se enviaría a la impresora térmica (mismo formato,
    /// mismos saltos de línea), sin conectarse a ninguna impresora real. Pensado para poder
    /// previsualizar en pantalla cómo se ve la comanda — por ejemplo, para presentarle el
    /// sistema al cliente sin depender de tener la ticketera física conectada.
    /// </summary>
    string PrevisualizarComanda(ComandaCocina comanda);
}

public record ItemComanda
{
    public string ProductoNombre { get; init; } = string.Empty;
    public int Cantidad { get; init; }
    public string? Notas { get; init; }
}

public record ComandaCocina
{
    public int PedidoId { get; init; }
    public string MesaNumero { get; init; } = string.Empty;
    public string MozoNombre { get; init; } = string.Empty;
    public DateTime CreadoEn { get; init; }

    // true cuando la comanda es por ítems agregados a un pedido ya existente (no el pedido
    // completo inicial), para que cocina la distinga claramente de la comanda original.
    public bool EsAdicional { get; init; }

    public List<ItemComanda> Items { get; init; } = new();
}
