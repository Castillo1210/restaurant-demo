using CaraNegra.Domain.Common;

namespace CaraNegra.Domain.Entities;

public class Rol : BaseEntity
{
    public int RolId { get; set;}
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;

    // Propiedad de navegacion
    public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
}