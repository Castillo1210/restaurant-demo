using CaraNegra.Domain.Common;

namespace CaraNegra.Domain.Entities;

public class Usuario : BaseEntity
{
    public int UsuarioId { get; set; }
    public string NombreUsuario { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool EsVerificado { get; set; }
    public DateTime? UltimoAccesoEn { get; set; }

    // Clave foranea
    public int RolId { get; set; }

    // Propiedades de navegacion
    public Rol Rol { get; set; } = null!;
    public ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();
    public ICollection<MovimientoArticulo> MovimientosArticulos { get; set; } = new List<MovimientoArticulo>();
}