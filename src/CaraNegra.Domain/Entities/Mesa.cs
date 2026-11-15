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

    // Salón / área a la que pertenece la mesa. Es nullable solo a nivel de base de datos para
    // que la migración no falle con mesas ya existentes (SalonSeeder las asigna a un
    // "Salón principal" al arrancar); crear o editar una mesa SIEMPRE exige un salón.
    public int? SalonId { get; set; }

    /// <summary>
    /// Mesa interna para las ventas de mostrador (Venta rápida): no aparece en el mapa de mesas,
    /// no tiene salón y nunca queda ocupada. La crea MostradorSeeder al arrancar.
    /// </summary>
    public bool EsMostrador { get; set; }

    // Propiedades de Navegacion
    public Salon? Salon { get; set; }
    public ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();
}