using CaraNegra.Application.Common;
using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Empresa;

public class DatosEmpresaProvider : IDatosEmpresaProvider
{
    private readonly IApplicationDbContext _context;
    private readonly DatosEmpresaCache _cache;

    public DatosEmpresaProvider(IApplicationDbContext context, DatosEmpresaCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<DatosEmpresa> ObtenerAsync(CancellationToken cancellationToken = default)
    {
        var enCache = _cache.Actual;
        if (enCache is not null) return enCache;

        var fila = await _context.ConfiguracionesEmpresa
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.ConfiguracionEmpresaId == ConfiguracionEmpresa.IdUnico, cancellationToken);

        if (fila is null) return _cache.PorDefecto;

        var datos = Mapear(fila, _cache.PorDefecto.ModoSimulacion);
        _cache.Actual = datos;
        return datos;
    }

    public void Invalidar() => _cache.Actual = null;

    public static DatosEmpresa Mapear(ConfiguracionEmpresa e, bool modoSimulacion) => new()
    {
        Ruc = e.Ruc,
        RazonSocial = e.RazonSocial,
        NombreComercial = e.NombreComercial,
        Direccion = e.Direccion,
        Telefono = e.Telefono,
        Email = e.Email,
        MensajePie = e.MensajePie,
        LogoBase64 = e.LogoBase64,
        SerieBoleta = e.SerieBoleta,
        SerieFactura = e.SerieFactura,
        IgvPorcentaje = e.IgvPorcentaje,
        ModoSimulacion = modoSimulacion
    };
}
