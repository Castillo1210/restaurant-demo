namespace CaraNegra.Domain.Common;

public abstract class BaseEntity
{
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public DateTime? ActualizadoEn { get; set; }
}