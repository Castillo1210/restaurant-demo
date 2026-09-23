using CaraNegra.Application.Productos.DTOs;
using CaraNegra.Application.Productos.Validators;
using FluentAssertions;
using Moq;
using Microsoft.EntityFrameworkCore;
using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Domain.Entities;
using Xunit;

namespace CaraNegra.Tests.Validators;

public class CreateProductoDtoValidatorTests
{
    private readonly CreateProductoDtoValidator _validator;

    public CreateProductoDtoValidatorTests()
    {
        var categorias = new List<Categoria>
        {
            new()
            {
                CategoriaId = 1,
                EstaActivo = true
            }
        }.AsQueryable();

        var mockSet = new Mock<DbSet<Categoria>>();

        mockSet.As<IQueryable<Categoria>>().Setup(m => m.Provider).Returns(categorias.Provider);
        mockSet.As<IQueryable<Categoria>>().Setup(m => m.Expression).Returns(categorias.Expression);
        mockSet.As<IQueryable<Categoria>>().Setup(m => m.ElementType).Returns(categorias.ElementType);
        mockSet.As<IQueryable<Categoria>>().Setup(m => m.GetEnumerator()).Returns(() => categorias.GetEnumerator());

        var mockContext = new Mock<IApplicationDbContext>();
        mockContext.Setup(c => c.Categorias).Returns(mockSet.Object);

        _validator = new CreateProductoDtoValidator(mockContext.Object);
    }

    [Fact]
    public void Validate_ValidDto_ShouldPass()
    {
        var dto = new CreateProductoDto
        {
            Nombre = "Coca Cola",
            Descripcion = "Refresco de cola",
            Precio = 25.50m,
            Tipo = "Bebida",
            CategoriaId = 1
        };

        var result = _validator.Validate(dto);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyNombre_ShouldFail()
    {
        var dto = new CreateProductoDto
        {
            Nombre = "",
            Descripcion = "Descripción válida",
            Precio = 10m,
            Tipo = "Bebida",
            CategoriaId = 1
        };

        var result = _validator.Validate(dto);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Nombre" && e.ErrorMessage.Contains("obligatorio"));
    }

    [Fact]
    public void Validate_PrecioZeroOrNegative_ShouldFail()
    {
        var dto = new CreateProductoDto
        {
            Nombre = "Producto",
            Precio = 0,
            Tipo = "Bebida",
            CategoriaId = 1
        };

        var result = _validator.Validate(dto);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Precio" && e.ErrorMessage.Contains("mayor a 0"));
    }

    [Fact]
    public void Validate_CategoriaIdZero_ShouldFail()
    {
        var dto = new CreateProductoDto
        {
            Nombre = "Producto",
            Precio = 10m,
            Tipo = "Bebida",
            CategoriaId = 0
        };

        var result = _validator.Validate(dto);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "CategoriaId" && e.ErrorMessage.Contains("obligatoria"));
    }
}