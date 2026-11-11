using CaraNegra.Application.Clientes.Commands;
using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaraNegra.API.Controllers;

/// <summary>
/// Clientes identificados por documento. La consulta busca primero en la base local y, solo si
/// el cliente es nuevo, en la API externa de DNI/RUC (el resultado se guarda para la próxima vez).
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/clientes")]
[ApiVersion("1.0")]
[Authorize(Roles = "CAJERO,ADMIN")]
public class ClientesController : ControllerBase
{
    private readonly IMediator _mediator;

    public ClientesController(IMediator mediator) => _mediator = mediator;

    /// <summary>GET /clientes/consulta?tipo=DNI&amp;numero=12345678 (tipo: DNI | RUC | CarnetExtranjeria)</summary>
    [HttpGet("consulta")]
    public async Task<IActionResult> Consultar([FromQuery] TipoDocumentoIdentidad tipo, [FromQuery] string numero)
    {
        try
        {
            return Ok(await _mediator.Send(new ConsultarDocumentoCommand(tipo, numero)));
        }
        catch (ConsultaDocumentoNoDisponibleException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { mensaje = ex.Message });
        }
    }
}
