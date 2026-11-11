using CaraNegra.Domain.Common;

namespace CaraNegra.Domain.Entities;

/// <summary>
/// Área o ambiente del restaurante (p.ej. "Terraza", "Jardín", "Salón principal", "Zona de
/// piscina"). Un restaurante campestre al aire libre tiene varias áreas y cada mesa pertenece
/// a una de ellas. La numeración de mesas es POR SALÓN: puede existir "Mesa 1" en Terraza y
/// "Mesa 1" en Jardín (índice único SalonId + NumeroMesa).
/// </summary>
public class Salon : BaseEntity
{
    public int SalonId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    /// <summary>Orden en que se muestran las pestañas de salones (menor primero).</summary>
    public int Orden { get; set; }

    /// <summary>Un salón inactivo (p.ej. cerrado por temporada) no aparece en el mapa del mozo.</summary>
    public bool EstaActivo { get; set; } = true;

    // Navegación
    public ICollection<Mesa> Mesas { get; set; } = new List<Mesa>();
}
