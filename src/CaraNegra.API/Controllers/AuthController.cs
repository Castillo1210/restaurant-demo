using CaraNegra.Application.Auth.Commands;
using CaraNegra.Application.Auth.DTOs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CaraNegra.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/auth")]
[ApiVersion("1.0")]
[EnableRateLimiting("AuthPolicy")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            var response = await _mediator.Send(new LoginCommand(request));
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { mensaje = ex.Message });
        }
    }

    /// <summary>
    /// Registra un nuevo usuario del personal. Solo un ADMIN autenticado puede crear cuentas
    /// (evita que cualquier persona anónima se autoasigne un rol, incluido ADMIN).
    /// </summary>
    [HttpPost("register")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var mensaje = await _mediator.Send(new RegisterCommand(request));
            return Ok(new { mensaje });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
    }
}