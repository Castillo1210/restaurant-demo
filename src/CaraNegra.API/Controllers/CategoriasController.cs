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
        var result = await _mediator.Send(new GetCategoriaByIdQuery(id));
        return Ok(result);
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
        var result = await _mediator.Send(new UpdateCategoriaCommand(id, dto));
        return Ok(result);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Delete(int id)
    {
        await _mediator.Send(new DeleteCategoriaCommand(id));
        return Ok(new { mensaje = "Categoría desactivada correctamente." });
    }
}