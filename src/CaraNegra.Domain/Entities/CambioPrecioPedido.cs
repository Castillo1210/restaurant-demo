namespace CaraNegra.Domain.Entities;

/// <summary>
/// Registro (auditoría) de un precio distinto al de la carta en un pedido: al agregarlo o
/// cambiándolo después. Solo lo hacen el cajero o el administrador y queda quién, cuándo,
/// de cuánto a cuánto y el motivo. Nunca se borra ni se edita.
/// </summary>
public class CambioPrecioPedido
{
    public int CambioPrecioPedidoId { get; set; }
    public int PedidoId { get; set; }
    public int? JornadaId { get; set; }

    /// <summary>Línea del pedido (sin relación en la BD: la línea puede quitarse después).</summary>
    public int DetallePedidoId { get; set; }
    public string ProductoNombre { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal PrecioAnterior { get; set; }
    public decimal PrecioNuevo { get; set; }
    public string? Motivo { get; set; }
    public string Mesa { get; set; } = string.Empty;

    public int UsuarioId { get; set; }
    public string UsuarioNombre { get; set; } = string.Empty;
    public string UsuarioRol { get; set; } = string.Empty;
    public DateTime CambiadoEn { get; set; }

    public Pedido Pedido { get; set; } = null!;
}
