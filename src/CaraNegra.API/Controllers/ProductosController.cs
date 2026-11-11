using CaraNegra.Application.Productos.Commands;
using CaraNegra.Application.Productos.DTOs;
using CaraNegra.Application.Productos.Imagenes;
using CaraNegra.Application.Productos.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

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
        var result = await _mediator.Send(new GetProductoByIdQuery(id));
        return Ok(result);
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
        var result = await _mediator.Send(new UpdateProductoCommand(id, dto));
        return Ok(result);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Delete(int id)
    {
        await _mediator.Send(new DeleteProductoCommand(id));
        return Ok(new { mensaje = "Producto desactivado correctamente." });
    }

    // ───────────── Foto del producto ─────────────

    /// <summary>
    /// Foto del producto. Pública (sin token) porque la pide la etiqueta &lt;img&gt;, que no envía
    /// el token; solo son fotos de la carta. Con ?v=N (la versión actual, viene en ProductoDto)
    /// el navegador la guarda en caché un año: al cambiar la foto cambia N y se descarga la nueva.
    /// </summary>
    [HttpGet("{id}/imagen")]
    [AllowAnonymous]
    public async Task<IActionResult> GetImagen(int id, [FromQuery] int? v)
    {
        var archivo = await _mediator.Send(new GetImagenProductoQuery(id));
        if (archivo is null) return NotFound();

        Response.Headers[HeaderNames.CacheControl] = v == archivo.Version
            ? "public, max-age=31536000, immutable"
            : "no-cache";
        var etag = new EntityTagHeaderValue($"\"{id}-{archivo.Version}\"");
        return File(archivo.Datos, archivo.TipoContenido, lastModified: null, entityTag: etag);
    }

    /// <summary>
    /// Sube o reemplaza la foto (multipart/form-data, campo "archivo"). El navegador la envía ya
    /// reducida; aquí se valida que sea JPG/PNG/WebP de verdad y que no pase de 1,5 MB.
    /// </summary>
    [HttpPut("{id}/imagen")]
    [Authorize(Roles = "ADMIN")]
    [RequestSizeLimit(2_000_000)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SubirImagen(int id, IFormFile? archivo)
    {
        if (archivo is null || archivo.Length == 0)
            return BadRequest(new { mensaje = "Selecciona una imagen" });
        if (archivo.Length > ImagenProductoReglas.MaximoBytes)
            return BadRequest(new { mensaje = "La imagen es muy pesada (máximo 1,5 MB)" });

        byte[] datos;
        using (var memoria = new MemoryStream((int)archivo.Length))
        {
            await archivo.CopyToAsync(memoria, HttpContext.RequestAborted);
            datos = memoria.ToArray();
        }

        var resultado = await _mediator.Send(new GuardarImagenProductoCommand(id, datos));
        return Ok(resultado);
    }

    [HttpDelete("{id}/imagen")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> QuitarImagen(int id)
    {
        return Ok(await _mediator.Send(new QuitarImagenProductoCommand(id)));
    }
}