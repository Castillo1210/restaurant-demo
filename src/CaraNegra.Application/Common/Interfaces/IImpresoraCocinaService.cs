namespace CaraNegra.Application.Common.Interfaces;

/// <summary>
/// Envía la comanda de un pedido a la impresora térmica de cocina conectada a la red local
/// (Fase 6). La implementación NUNCA debe lanzar una excepción: si la impresora está apagada,
/// desconectada, o no está configurada, devuelve el resultado, lo registra en el log y avisa
/// a las pantallas — imprimir no debe poder bloquear ni hacer fallar la toma de pedidos, pero
/// la cocina trabaja con los tickets, así que una comanda que no salió no puede pasar inadvertida.
/// </summary>
public interface IImpresoraCocinaService
{
    Task<ResultadoImpresion> ImprimirComandaAsync(ComandaCocina comanda, CancellationToken cancellationToken = default);

    /// <summary>
    /// Devuelve el texto exacto que se enviaría a la impresora térmica (mismo formato,
    /// mismos saltos de línea), sin conectarse a ninguna impresora real. Pensado para poder
    /// previsualizar en pantalla cómo se ve la comanda — por ejemplo, para presentarle el
    /// sistema al cliente sin depender de tener la ticketera física conectada.
    /// </summary>
    string PrevisualizarComanda(ComandaCocina comanda);
}

public enum ResultadoImpresion
{
    /// <summary>El ticket se entregó a la impresora.</summary>
    Impresa,
    /// <summary>La impresora está desactivada o sin IP en la configuración (p. ej. la demo).</summary>
    Deshabilitada,
    /// <summary>La impresora no respondió (apagada, desconectada, IP equivocada…).</summary>
    Fallo
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
    public string SalonNombre { get; init; } = string.Empty;
    public string MozoNombre { get; init; } = string.Empty;
    public DateTime CreadoEn { get; init; }

    // true cuando la comanda es por ítems agregados a un pedido ya existente (no el pedido
    // completo inicial), para que cocina la distinga claramente de la comanda original.
    public bool EsAdicional { get; init; }

    // true cuando se QUITARON productos ya enviados: cocina debe dejar de prepararlos.
    // MozoNombre es entonces quien anuló y Motivo el porqué.
    public bool EsAnulacion { get; init; }
    public string? Motivo { get; init; }

    // Con EsAnulacion: se anuló el PEDIDO COMPLETO (la mesa quedó libre), no solo algunos platos.
    public bool EsPedidoCompleto { get; init; }

    // Comentario general del pedido ("todo para llevar"): se imprime destacado antes de los platos.
    public string? Comentario { get; init; }

    // true cuando solo cambió el comentario de un pedido ya enviado: el ticket lleva solo
    // el comentario nuevo (sin platos), para que cocina se entere. MozoNombre es quien lo cambió.
    public bool EsCambioComentario { get; init; }

    // true cuando se vuelve a imprimir a pedido (botón "Reimprimir"): el ticket lo dice arriba,
    // para que cocina no lo tome como un pedido nuevo.
    public bool EsReimpresion { get; init; }

    // true para los pedidos que manda el ADMINISTRADOR o el CAJERO: la comanda NO sale en la
    // ticketera de cocina; la imprime el propio equipo que envió el pedido en su ticketera
    // (impresora predeterminada de Windows), igual que la boleta.
    public bool ParaCaja { get; init; }

    public List<ItemComanda> Items { get; init; } = new();
}
