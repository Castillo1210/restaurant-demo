using CaraNegra.Domain.Common;
using CaraNegra.Domain.Enums;

namespace CaraNegra.Domain.Entities;

public class Pedido : BaseEntity
{
    public int PedidoId { get; set; }
    public decimal SubTotal { get; set; }
    public decimal Total { get; set; }
    public EstadoPedido EstadoPedido { get; set; } = EstadoPedido.Pendiente;

    // Claves foraneas
    public int MesaId { get; set; }
    public int UsuarioId { get; set; }

    // Propiedades de navegacion
    public Mesa Mesa { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
    public ICollection<DetallePedido> DetallesPedidos { get; set; } = new List<DetallePedido>();
    public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
    public ICollection<DetalleDescuento> DetallesDescuentos { get; set; } = new List<DetalleDescuento>();
    public ICollection<Comprobante> Comprobantes { get; set; } = new List<Comprobante>();
}