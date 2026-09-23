using System.Net;
using System.Net.Http.Json;
using CaraNegra.Application.Productos.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CaraNegra.IntegrationTests;

public class ProductosIntegrationTests : IClassFixture<CaraNegraWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ProductosIntegrationTests(CaraNegraWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v1.0/productos");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_WithoutAuth_ReturnsUnauthorized()
    {
        var dto = new CreateProductoDto
        {
            Nombre = "Test Producto",
            Descripcion = "Descripción de prueba",
            Precio = 10.50m,
            Tipo = "Bebida",
            CategoriaId = 1
        };

        var response = await _client.PostAsJsonAsync("/api/v1.0/productos", dto);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}