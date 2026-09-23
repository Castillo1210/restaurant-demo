using CaraNegra.Domain.Common;

namespace CaraNegra.Domain.Entities;

public class Pago : BaseEntity
{
    public int PagoId { get; set; }
    public decimal Monto { get; set; }
    public string? Referencia { get; set; }

    // Auditoría de anulación: nunca se borra el registro (soft-void), para mantener
    // trazabilidad de caja (quién anuló, cuándo y por qué).
    public bool EstaAnulado { get; set; } = false;
    public string? MotivoAnulacion { get; set; }
    public DateTime? AnuladoEn { get; set; }
    public int? AnuladoPorUsuarioId { get; set; }

    // Claves foraneas
    public int PedidoId { get; set; }
    public int MetodoPagoId { get; set; }

    // Propiedades de navegación
    public Pedido Pedido { get; set; } = null!;
    public MetodoPago MetodoPago { get; set; } = null!;
}