using CaraNegra.Domain.Common;
using CaraNegra.Domain.Enums;

namespace CaraNegra.Domain.Entities;

public class DetallePedido : BaseEntity
{
    public int DetallePedidoId { get; set; }
    public int Cantidad { get; set; }
    public decimal Monto { get; set; } // Precio unitario
    public string? Notas { get; set; }
    public EstadoDetallePedido EstadoDetallePedido { get; set; } = EstadoDetallePedido.Pendiente;

    // Claves foraneas
    public int PedidoId { get; set; }
    public int ProductoId { get; set; }

    // Propiedades de navegacion
    public Pedido Pedido { get; set; } = null!;
    public Producto Producto { get; set; } = null!;
}