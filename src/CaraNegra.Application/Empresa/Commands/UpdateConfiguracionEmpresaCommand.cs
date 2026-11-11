using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Empresa.DTOs;
using CaraNegra.Application.Empresa.Queries;
using CaraNegra.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Empresa.Commands;

/// <summary>Guarda los datos de la empresa (crea la fila única si no existe) y refresca la caché.</summary>
public record UpdateConfiguracionEmpresaCommand(UpdateConfiguracionEmpresaDto Dto) : IRequest<ConfiguracionEmpresaDto>;

public class UpdateConfiguracionEmpresaCommandHandler : IRequestHandler<UpdateConfiguracionEmpresaCommand, ConfiguracionEmpresaDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IDatosEmpresaProvider _provider;
    private readonly IMediator _mediator;

    public UpdateConfiguracionEmpresaCommandHandler(IApplicationDbContext context, IDatosEmpresaProvider provider, IMediator mediator)
    {
        _context = context;
        _provider = provider;
        _mediator = mediator;
    }

    public async Task<ConfiguracionEmpresaDto> Handle(UpdateConfiguracionEmpresaCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;

        var fila = await _context.ConfiguracionesEmpresa
            .FirstOrDefaultAsync(e => e.ConfiguracionEmpresaId == ConfiguracionEmpresa.IdUnico, cancellationToken);

        if (fila is null)
        {
            fila = new ConfiguracionEmpresa { ConfiguracionEmpresaId = ConfiguracionEmpresa.IdUnico };
            _context.ConfiguracionesEmpresa.Add(fila);
        }

        static string? Opcional(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        fila.Ruc = dto.Ruc.Trim();
        fila.RazonSocial = dto.RazonSocial.Trim();
        fila.NombreComercial = dto.NombreComercial.Trim();
        fila.Direccion = dto.Direccion.Trim();
        fila.Telefono = Opcional(dto.Telefono);
        fila.Email = Opcional(dto.Email);
        fila.SerieBoleta = dto.SerieBoleta.Trim().ToUpperInvariant();
        fila.SerieFactura = dto.SerieFactura.Trim().ToUpperInvariant();
        fila.IgvPorcentaje = dto.IgvPorcentaje;
        fila.MensajePie = Opcional(dto.MensajePie);
        fila.LogoBase64 = Opcional(dto.LogoBase64);

        await _context.SaveChangesAsync(cancellationToken);
        _provider.Invalidar();

        return await _mediator.Send(new GetConfiguracionEmpresaQuery(), cancellationToken);
    }
}
