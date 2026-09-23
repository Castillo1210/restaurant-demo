using CaraNegra.Domain.Common;
using CaraNegra.Domain.Enums;

namespace CaraNegra.Domain.Entities;

public class MovimientoArticulo : BaseEntity
{
    public int MovimientoArticuloId { get; set; }
    public TipoMovimiento TipoMovimiento { get; set; }
    public int Cantidad { get; set; }
    public int Balance { get; set; }
    public string? ReferenciaCod { get; set; }
    public string? File { get; set; } // Ruta del archivo
    public string? Notas { get; set; }

    // Claves foraneas
    public int ArticuloId { get; set; }
    public int UsuarioId { get; set; }

    // Propiedades de navegacion
    public Articulo Articulo { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
}