using CaraNegra.Application.Productos.Commands;
using CaraNegra.Application.Productos.DTOs;
using CaraNegra.Application.Productos.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaraNegra.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/productos")]
[ApiVersion("1.0")]
[Authorize]
public class ProductosController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProductosController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool soloDisponibles = true, [FromQuery] int? categoriaId = null)
    {
        var result = await _mediator.Send(new GetAllProductosQuery(soloDisponibles, categoriaId));
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var result = await _mediator.Send(new GetProductoByIdQuery(id));
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Create([FromBody] CreateProductoDto dto)
    {
        var result = await _mediator.Send(new CreateProductoCommand(dto));
        return CreatedAtAction(nameof(GetById), new { id = result.ProductoId }, result);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateProductoDto dto)
    {
        try
        {
            var result = await _mediator.Send(new UpdateProductoCommand(id, dto));
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
            await _mediator.Send(new DeleteProductoCommand(id));
            return Ok(new { mensaje = "Producto desactivado correctamente." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
    }
}