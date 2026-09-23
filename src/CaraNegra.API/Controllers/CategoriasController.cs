using CaraNegra.Application.Categorias.Commands;
using CaraNegra.Application.Categorias.DTOs;
using CaraNegra.Application.Categorias.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaraNegra.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/categorias")]
[ApiVersion("1.0")]
[Authorize]
public class CategoriasController : ControllerBase
{
    private readonly IMediator _mediator;

    public CategoriasController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool soloActivas = true)
    {
        var result = await _mediator.Send(new GetAllCategoriasQuery(soloActivas));
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var result = await _mediator.Send(new GetCategoriaByIdQuery(id));
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Create([FromBody] CreateCategoriaDto dto)
    {
        var result = await _mediator.Send(new CreateCategoriaCommand(dto));
        return CreatedAtAction(nameof(GetById), new { id = result.CategoriaId }, result);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCategoriaDto dto)
    {
        try
        {
            var result = await _mediator.Send(new UpdateCategoriaCommand(id, dto));
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _mediator.Send(new DeleteCategoriaCommand(id));
            return Ok(new { mensaje = "Categoría desactivada correctamente." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
    }
}