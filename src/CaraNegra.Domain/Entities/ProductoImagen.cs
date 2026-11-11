namespace CaraNegra.Domain.Entities;

/// <summary>
/// Foto de un producto (una por producto). Se guarda en la base de datos y no en una carpeta:
/// así entra sola en el respaldo diario, se restaura con él y sobrevive a las actualizaciones
/// del sistema. Llega ya comprimida desde el navegador (unos 30-80 KB por foto).
/// </summary>
public class ProductoImagen
{
    public int ProductoId { get; set; }
    public byte[] Datos { get; set; } = Array.Empty<byte>();
    public string TipoContenido { get; set; } = "image/jpeg";
    public DateTime ActualizadoEn { get; set; }

    public Producto Producto { get; set; } = null!;
}
