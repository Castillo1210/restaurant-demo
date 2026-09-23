using CaraNegra.Domain.Common;

namespace CaraNegra.Domain.Entities;

public class DetalleDescuento : BaseEntity
{
    public int DetalleDescuentoId { get; set; }

    // Claves foraneas
    public int PedidoId { get; set; }
    public int Descuentoid { get; set; }

    // Propiedades de navegación
    public Pedido Pedido { get; set; } = null!;
    public Descuento Descuento { get; set; } = null!;
}