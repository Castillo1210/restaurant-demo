using CaraNegra.Application.Categorias.DTOs;
using CaraNegra.Application.Categorias.Validators;
using FluentAssertions;
using Xunit;

namespace CaraNegra.Tests.Validators;

public class CreateCategoriaDtoValidatorTests
{
    private readonly CreateCategoriaDtoValidator _validator = new();

    [Fact]
    public void Validate_ValidDto_ShouldPass()
    {
        var dto = new CreateCategoriaDto
        {
            Nombre = "Bebidas",
            Descripcion = "Todo tipo de bebidas"
        };

        var result = _validator.Validate(dto);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyNombre_ShouldFail()
    {
        var dto = new CreateCategoriaDto
        {
            Nombre = "",
            Descripcion = "Descripción válida"
        };

        var result = _validator.Validate(dto);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Nombre" && e.ErrorMessage.Contains("obligatorio"));
    }

    [Fact]
    public void Validate_NombreTooLong_ShouldFail()
    {
        var dto = new CreateCategoriaDto
        {
            Nombre = new string('a', 101),
            Descripcion = "Descripción válida"
        };

        var result = _validator.Validate(dto);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Nombre" && e.ErrorMessage.Contains("100 caracteres"));
    }

    [Fact]
    public void Validate_DescripcionTooLong_ShouldFail()
    {
        var dto = new CreateCategoriaDto
        {
            Nombre = "Bebidas",
            Descripcion = new string('a', 501)
        };

        var result = _validator.Validate(dto);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Descripcion" && e.ErrorMessage.Contains("500 caracteres"));
    }
}