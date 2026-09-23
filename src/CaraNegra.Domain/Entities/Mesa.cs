using CaraNegra.Domain.Common;
using CaraNegra.Domain.Enums;

namespace CaraNegra.Domain.Entities;

public class Mesa : BaseEntity
{
    public int MesaId { get; set; }
    // Es un código de mesa definido por el usuario (no necesariamente numérico correlativo),
    // por eso es string y no int — ver CONFIGURACION_LOCAL.md.
    public string NumeroMesa { get; set; } = string.Empty;
    public EstadoMesa Estado { get; set; } = EstadoMesa.Disponible;

    // Propiedad de Navegacion
    public ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();
}