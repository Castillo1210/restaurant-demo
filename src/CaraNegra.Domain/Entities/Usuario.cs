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

    // ── Ingreso rápido con PIN (solo MOZO y COCINA, y solo desde dispositivos registrados) ──

    /// <summary>Hash BCrypt del PIN de 4 dígitos. Null = el usuario no tiene PIN.</summary>
    public string? PinHash { get; set; }

    /// <summary>PINs incorrectos seguidos. Al llegar al máximo se bloquea el PIN unos minutos.</summary>
    public int PinIntentosFallidos { get; set; }

    /// <summary>Si tiene valor y aún no pasó, el PIN está bloqueado (la contraseña sigue funcionando).</summary>
    public DateTime? PinBloqueadoHasta { get; set; }

    // Clave foranea
    public int RolId { get; set; }

    // Propiedades de navegacion
    public Rol Rol { get; set; } = null!;
    public ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();
    public ICollection<MovimientoArticulo> MovimientosArticulos { get; set; } = new List<MovimientoArticulo>();
}