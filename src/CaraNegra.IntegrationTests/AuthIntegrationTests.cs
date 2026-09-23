using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CaraNegra.Application.Auth.DTOs;
using CaraNegra.Application.Auth.Interfaces;
using CaraNegra.Domain.Entities;
using CaraNegra.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CaraNegra.IntegrationTests;

public class AuthIntegrationTests : IClassFixture<CaraNegraWebApplicationFactory>
{
    private readonly CaraNegraWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthIntegrationTests(CaraNegraWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthCheck_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Register_WithoutAuth_ReturnsUnauthorized()
    {
        // El registro ya no es público: se requiere un token de ADMIN (ver AuthController).
        var request = new RegisterRequest
        {
            NombreUsuario = "testuser_sin_auth",
            NombreCompleto = "Test User",
            Password = "Password123!",
            RolId = 1
        };

        var response = await _client.PostAsJsonAsync("/api/v1.0/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Register_AsAdmin_ReturnsOk()
    {
        var (rolAdminId, _) = await SeedAdminAsync("admin_register_test", "AdminPass123!");
        var token = await LoginAndGetTokenAsync("admin_register_test", "AdminPass123!");

        var request = new RegisterRequest
        {
            NombreUsuario = "nuevo_mozo_test",
            NombreCompleto = "Mozo de Prueba",
            Password = "Password123!",
            RolId = rolAdminId
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1.0/auth/register")
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(httpRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Register_InvalidRequest_AsAdmin_ReturnsBadRequest()
    {
        var (_, _) = await SeedAdminAsync("admin_register_invalid_test", "AdminPass123!");
        var token = await LoginAndGetTokenAsync("admin_register_invalid_test", "AdminPass123!");

        var request = new RegisterRequest
        {
            NombreUsuario = "ab",
            NombreCompleto = "Test",
            Password = "weak",
            RolId = 0
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1.0/auth/register")
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(httpRequest);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_InvalidCredentials_ReturnsUnauthorized()
    {
        var request = new LoginRequest
        {
            NombreUsuario = "nonexistent",
            Password = "wrongpassword"
        };

        var response = await _client.PostAsJsonAsync("/api/v1.0/auth/login", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Crea (si no existe) el rol ADMIN y un usuario administrador directamente en la
    /// base de datos de pruebas, para poder autenticarse y ejercitar endpoints protegidos.
    /// </summary>
    private async Task<(int RolId, int UsuarioId)> SeedAdminAsync(string nombreUsuario, string password)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();

        var rolAdmin = context.Roles.FirstOrDefault(r => r.Nombre == "ADMIN");
        if (rolAdmin == null)
        {
            rolAdmin = new Rol { Nombre = "ADMIN", Descripcion = "Administrador" };
            context.Roles.Add(rolAdmin);
            await context.SaveChangesAsync();
        }

        if (!context.Usuarios.Any(u => u.NombreUsuario == nombreUsuario))
        {
            context.Usuarios.Add(new Usuario
            {
                NombreUsuario = nombreUsuario,
                NombreCompleto = "Admin de Prueba",
                PasswordHash = passwordService.HashPassword(password),
                RolId = rolAdmin.RolId,
                EsVerificado = true
            });
            await context.SaveChangesAsync();
        }

        var usuario = context.Usuarios.First(u => u.NombreUsuario == nombreUsuario);
        return (rolAdmin.RolId, usuario.UsuarioId);
    }

    private async Task<string> LoginAndGetTokenAsync(string nombreUsuario, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/v1.0/auth/login", new LoginRequest
        {
            NombreUsuario = nombreUsuario,
            Password = password
        });

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("token").GetString()!;
    }
}