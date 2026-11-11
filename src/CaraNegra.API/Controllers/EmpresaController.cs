using CaraNegra.Application.Empresa.Commands;
using CaraNegra.Application.Empresa.DTOs;
using CaraNegra.Application.Empresa.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaraNegra.API.Controllers;

/// <summary>
/// Datos de la empresa (RUC, razón social, nombre comercial, dirección, series, IGV, logo...).
/// Se editan desde Admin → Datos de la empresa; así el sistema se puede instalar en otro
/// restaurante sin cambiar código.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/empresa")]
[ApiVersion("1.0")]
[Authorize(Roles = "ADMIN")]
public class EmpresaController : ControllerBase
{
    private readonly IMediator _mediator;

    public EmpresaController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        return Ok(await _mediator.Send(new GetConfiguracionEmpresaQuery()));
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateConfiguracionEmpresaDto dto)
    {
        return Ok(await _mediator.Send(new UpdateConfiguracionEmpresaCommand(dto)));
    }

    /// <summary>Nombre comercial y logo, sin sesión (pantalla de login y título de la app).</summary>
    [HttpGet("publico")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublico()
    {
        return Ok(await _mediator.Send(new GetEmpresaPublicaQuery()));
    }
}
