namespace CaraNegra.API.Impresion;

/// <summary>
/// Se enlaza a la sección "ImpresoraCocina" de appsettings.json. Todo vacío/deshabilitado
/// por defecto: hasta que alguien configure la IP real de la impresora térmica de cocina,
/// el sistema simplemente no intenta imprimir (ver ImpresoraCocinaService).
/// </summary>
public class ImpresoraCocinaOptions
{
    public bool Habilitada { get; set; }
    public string Ip { get; set; } = string.Empty;
    public int Puerto { get; set; } = 9100; // Puerto RAW/JetDirect estándar en impresoras ESC/POS de red
    public int TimeoutMs { get; set; } = 3000;

    /// <summary>
    /// Caracteres por línea en letra normal: 42 sirve en casi todas las de 80 mm (algunas
    /// aceptan 48); para 58 mm usar 32. Los productos en letra grande usan la mitad.
    /// </summary>
    public int Columnas { get; set; } = 42;

    /// <summary>
    /// Tamaño de los productos en la comanda: "Grande" (doble alto y ancho), "Alto" (doble
    /// alto, entra más texto por línea; por defecto) o "Normal" (negrita en letra normal).
    /// </summary>
    public string TamanoProductos { get; set; } = "Alto";
}
