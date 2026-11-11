using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Empresa.DTOs;
using CaraNegra.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Empresa.Queries;

public record GetConfiguracionEmpresaQuery : IRequest<ConfiguracionEmpresaDto>;

public class GetConfiguracionEmpresaQueryHandler : IRequestHandler<GetConfiguracionEmpresaQuery, ConfiguracionEmpresaDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IDatosEmpresaProvider _provider;

    public GetConfiguracionEmpresaQueryHandler(IApplicationDbContext context, IDatosEmpresaProvider provider)
    {
        _context = context;
        _provider = provider;
    }

    public async Task<ConfiguracionEmpresaDto> Handle(GetConfiguracionEmpresaQuery request, CancellationToken cancellationToken)
    {
        var datos = await _provider.ObtenerAsync(cancellationToken);
        var actualizadoEn = await _context.ConfiguracionesEmpresa
            .Where(e => e.ConfiguracionEmpresaId == ConfiguracionEmpresa.IdUnico)
            .Select(e => e.ActualizadoEn ?? e.CreadoEn)
            .FirstOrDefaultAsync(cancellationToken);

        return new ConfiguracionEmpresaDto
        {
            Ruc = datos.Ruc,
            RazonSocial = datos.RazonSocial,
            NombreComercial = datos.NombreComercial,
            Direccion = datos.Direccion,
            Telefono = datos.Telefono,
            Email = datos.Email,
            SerieBoleta = datos.SerieBoleta,
            SerieFactura = datos.SerieFactura,
            IgvPorcentaje = datos.IgvPorcentaje,
            MensajePie = datos.MensajePie,
            LogoBase64 = datos.LogoBase64,
            ModoSimulacion = datos.ModoSimulacion,
            ActualizadoEn = actualizadoEn == default ? null : actualizadoEn
        };
    }
}

public record GetEmpresaPublicaQuery : IRequest<EmpresaPublicaDto>;

public class GetEmpresaPublicaQueryHandler : IRequestHandler<GetEmpresaPublicaQuery, EmpresaPublicaDto>
{
    private readonly IDatosEmpresaProvider _provider;

    public GetEmpresaPublicaQueryHandler(IDatosEmpresaProvider provider) => _provider = provider;

    public async Task<EmpresaPublicaDto> Handle(GetEmpresaPublicaQuery request, CancellationToken cancellationToken)
    {
        var datos = await _provider.ObtenerAsync(cancellationToken);
        return new EmpresaPublicaDto
        {
            NombreComercial = datos.NombreComercial,
            Ruc = datos.Ruc,
            RazonSocial = datos.RazonSocial,
            Direccion = datos.Direccion,
            LogoBase64 = datos.LogoBase64,
            MensajePie = datos.MensajePie
        };
    }
}
