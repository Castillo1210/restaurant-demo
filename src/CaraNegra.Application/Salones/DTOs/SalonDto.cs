namespace CaraNegra.Application.Salones.DTOs;

public class SalonDto
{
    public int SalonId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int Orden { get; set; }
    public bool EstaActivo { get; set; }
    /// <summary>Mesas del salón (sin contar las que están en Mantenimiento/desactivadas).</summary>
    public int CantidadMesas { get; set; }
    public DateTime CreadoEn { get; set; }
}

public class CreateSalonDto
{
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int Orden { get; set; }
}

public class UpdateSalonDto
{
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int Orden { get; set; }
    public bool EstaActivo { get; set; } = true;
}
