using CaraNegra.API.Impresion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CaraNegra.API.Controllers;

/// <summary>
/// Formato de la comanda: la que imprime el navegador en caja (admin / cajero) usa las mismas
/// medidas que la ticketera de cocina (ImpresoraCocina:TamanoProductos).
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/impresion")]
[ApiVersion("1.0")]
[Authorize]
public class ImpresionController : ControllerBase
{
    private readonly IOptionsMonitor<ImpresoraCocinaOptions> _options;

    public ImpresionController(IOptionsMonitor<ImpresoraCocinaOptions> options) => _options = options;

    [HttpGet("comanda")]
    public IActionResult Comanda() => Ok(ImpresoraCocinaService.Formato(_options.CurrentValue));
}
