using CaraNegra.Domain.Common;
using CaraNegra.Domain.Enums;

namespace CaraNegra.Domain.Entities;

public class Pedido : BaseEntity
{
    public int PedidoId { get; set; }
    public decimal SubTotal { get; set; }
    public decimal Total { get; set; }
    public EstadoPedido EstadoPedido { get; set; } = EstadoPedido.Pendiente;

    /// <summary>
    /// Comentario general del pedido ("todo para llevar", "cliente apurado", "servir juntos").
    /// Distinto de las notas de cada plato. Sale en la comanda y en la pantalla de cocina.
    /// </summary>
    public string? Comentario { get; set; }

    // Claves foraneas
    public int MesaId { get; set; }
    public int UsuarioId { get; set; }

    /// <summary>Jornada (día operativo) en la que se registró. Null en datos anteriores a las jornadas.</summary>
    public int? JornadaId { get; set; }

    // Propiedades de navegacion
    public Mesa Mesa { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
    public ICollection<DetallePedido> DetallesPedidos { get; set; } = new List<DetallePedido>();
    public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
    public ICollection<DetalleDescuento> DetallesDescuentos { get; set; } = new List<DetalleDescuento>();
    public ICollection<Comprobante> Comprobantes { get; set; } = new List<Comprobante>();
}