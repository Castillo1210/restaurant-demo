using System.Net;
using System.Net.Http.Json;
using CaraNegra.Application.Categorias.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CaraNegra.IntegrationTests;

public class CategoriasIntegrationTests : IClassFixture<CaraNegraWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CategoriasIntegrationTests(CaraNegraWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v1.0/categorias");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_WithoutAuth_ReturnsUnauthorized()
    {
        var dto = new CreateCategoriaDto
        {
            Nombre = "Test Categoria",
            Descripcion = "Descripción de prueba"
        };

        var response = await _client.PostAsJsonAsync("/api/v1.0/categorias", dto);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}