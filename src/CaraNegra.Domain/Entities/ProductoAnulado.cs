namespace CaraNegra.Domain.Entities;

/// <summary>
/// Registro (auditoría) de un producto quitado de un pedido ya enviado a cocina: quién lo hizo,
/// cuándo, por qué, cuánto y en qué estado estaba en cocina. Nunca se borra ni se edita: sirve
/// para el control del administrador y aparece en el reporte de cierre de la jornada.
/// </summary>
public class ProductoAnulado
{
    public int ProductoAnuladoId { get; set; }
    public int PedidoId { get; set; }
    public int? JornadaId { get; set; }

    public int ProductoId { get; set; }
    public string ProductoNombre { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Importe { get; set; }

    /// <summary>Estado del ítem en cocina al anularlo (Pendiente, EnPreparacion, Listo…).</summary>
    public string EstadoItem { get; set; } = string.Empty;
    public string Motivo { get; set; } = string.Empty;

    /// <summary>"Terraza 4": la mesa tal como se veía al anular.</summary>
    public string Mesa { get; set; } = string.Empty;

    public int UsuarioId { get; set; }
    public string UsuarioNombre { get; set; } = string.Empty;
    public string UsuarioRol { get; set; } = string.Empty;
    public DateTime AnuladoEn { get; set; }

    public Pedido Pedido { get; set; } = null!;
}
