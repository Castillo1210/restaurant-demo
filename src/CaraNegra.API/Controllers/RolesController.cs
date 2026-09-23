using CaraNegra.Application.Roles.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaraNegra.API.Controllers;

/// <summary>
/// Solo lectura: expone los roles existentes (ADMIN, MOZO, CAJERO) para que el panel de
/// administración pueda armar el selector de rol al crear/editar un usuario. No hay
/// crear/editar/borrar rol porque la autorización de la aplicación usa el nombre del rol
/// hardcodeado en cada controlador — un rol nuevo no tendría ningún permiso real asociado.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/roles")]
[ApiVersion("1.0")]
[Authorize(Roles = "ADMIN")]
public class RolesController : ControllerBase
{
    private readonly IMediator _mediator;

    public RolesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _mediator.Send(new GetAllRolesQuery());
        return Ok(result);
    }
}
