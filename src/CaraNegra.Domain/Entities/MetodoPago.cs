using CaraNegra.Domain.Common;

namespace CaraNegra.Domain.Entities;

public class MetodoPago : BaseEntity, ISoftDeletable
{
    public int MetodoPagoId { get; set; }
    public string Nombre { get; set; } = string.Empty;

    // Permite "desactivar" un método de pago (p.ej. dejar de aceptar Yape) sin
    // borrarlo físicamente, ya que los pagos históricos siguen referenciándolo.
    public bool EstaActivo { get; set; } = true;

    // Propiedad de navegacion
    public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
}