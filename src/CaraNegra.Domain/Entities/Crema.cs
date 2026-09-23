using CaraNegra.Domain.Common;

namespace CaraNegra.Domain.Entities;

/// <summary>
/// Catálogo administrable de cremas/toppings (Fase 8) que se ofrecen como chips al armar
/// un pedido (ej. Mayonesa, Ketchup, BBQ). La elección del cliente sigue guardándose como
/// texto libre en DetallePedido.Notas — esta entidad solo controla QUÉ opciones se pueden
/// elegir, no estructura la selección en sí.
/// </summary>
public class Crema : BaseEntity, ISoftDeletable
{
    public int CremaId { get; set; }
    public string Nombre { get; set; } = string.Empty;

    // Controla el orden en que aparecen los chips en la app del mozo (menor primero).
    public int Orden { get; set; }

    // Permite "apagar" una crema (se acabó el insumo, se descontinuó) sin borrarla
    // físicamente ni afectar pedidos históricos que ya la mencionan en sus notas.
    public bool EstaActivo { get; set; } = true;
}
