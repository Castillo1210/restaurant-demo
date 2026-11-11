using CaraNegra.Application.Mesas.Commands;
using CaraNegra.Application.Mesas.DTOs;
using CaraNegra.Application.Mesas.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaraNegra.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/mesas")]
[ApiVersion("1.0")]
[Authorize(Roles = "MOZO,CAJERO,ADMIN")]
public class MesasController : ControllerBase
{
    private readonly IMediator _mediator;

    public MesasController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Obtiene todas las mesas
    /// </summary>
    /// <param name="soloDisponibles">Si true, solo retorna mesas con estado Disponible</param>
    /// <param name="salonId">Filtra por salón (opcional)</param>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool soloDisponibles = false, [FromQuery] int? salonId = null)
    {
        var result = await _mediator.Send(new GetAllMesasQuery(soloDisponibles, salonId));
        return Ok(result);
    }

    /// <summary>
    /// Obtiene una mesa por su ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _mediator.Send(new GetMesaByIdQuery(id));
        return Ok(result);
    }

    /// <summary>
    /// Crea una nueva mesa
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Create([FromBody] CreateMesaDto dto)
    {
        var result = await _mediator.Send(new CreateMesaCommand(dto));
        return CreatedAtAction(nameof(GetById), new { id = result.MesaId }, result);
    }

    /// <summary>
    /// Actualiza una mesa existente
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateMesaDto dto)
    {
        var result = await _mediator.Send(new UpdateMesaCommand(id, dto));
        return Ok(result);
    }

    /// <summary>
    /// Desactiva una mesa (soft delete - cambia estado a Mantenimiento)
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Delete(int id)
    {
        await _mediator.Send(new DeleteMesaCommand(id));
        return Ok(new { mensaje = "Mesa desactivada correctamente (estado: Mantenimiento)." });
    }
}