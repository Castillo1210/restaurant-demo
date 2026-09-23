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
}
