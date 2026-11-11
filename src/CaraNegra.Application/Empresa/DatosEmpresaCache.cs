using CaraNegra.Application.Common;

namespace CaraNegra.Application.Empresa;

/// <summary>
/// Caché en memoria (singleton) de los datos de la empresa: se leen de la BD una vez y se
/// reutilizan en cada comprobante/comanda hasta que el admin los edita. <see cref="PorDefecto"/>
/// son los valores de appsettings.json, usados solo si la tabla aún está vacía.
/// </summary>
public class DatosEmpresaCache
{
    private readonly object _lock = new();
    private DatosEmpresa? _actual;

    public DatosEmpresaCache(DatosEmpresa porDefecto) => PorDefecto = porDefecto;

    public DatosEmpresa PorDefecto { get; }

    /// <summary>Últimos datos leídos de la BD (null si todavía no se leyeron o se invalidaron).</summary>
    public DatosEmpresa? Actual
    {
        get { lock (_lock) return _actual; }
        set { lock (_lock) _actual = value; }
    }

    /// <summary>Nombre comercial para encabezados (comanda, recibo) aunque la caché esté vacía.</summary>
    public string NombreComercial => Actual?.NombreComercial ?? PorDefecto.NombreComercial;
}
