using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CaraNegra.Application.Auth.Interfaces;
using CaraNegra.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CaraNegra.Infrastructure.Services;

public class JwtService : IJwtService
{
    private readonly IConfiguration _configuration;

    public JwtService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerarToken(Usuario usuario)
    {
        var secret = _configuration["JwtSettings:Secret"]!;
        var issuer = _configuration["JwtSettings:Issuer"]!;
        var audience = _configuration["JwtSettings:Audience"]!;
        var expiresIn = int.Parse(_configuration["JwtSettings:ExpiresInMinutes"] ?? "10");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, usuario.UsuarioId.ToString()),
            new Claim("usuarioId", usuario.UsuarioId.ToString()),
            new Claim(ClaimTypes.Name, usuario.NombreUsuario),
            new Claim(ClaimTypes.GivenName, usuario.NombreCompleto),
            new Claim(ClaimTypes.Role, usuario.Rol?.Nombre ?? "")
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: ObtenerExpiracion(),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public DateTime ObtenerExpiracion()
    {
        var expiresIn = int.Parse(_configuration["JwtSettings:ExpiresInMinutes"] ?? "10");
        return DateTime.UtcNow.AddMinutes(expiresIn);
    }
}