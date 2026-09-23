using CaraNegra.Application.Categorias.DTOs;
using CaraNegra.Application.Categorias.Validators;
using FluentAssertions;
using Xunit;

namespace CaraNegra.Tests.Validators;

public class UpdateCategoriaDtoValidatorTests
{
    private readonly UpdateCategoriaDtoValidator _validator = new();

    [Fact]
    public void Validate_ValidDto_ShouldPass()
    {
        var dto = new UpdateCategoriaDto
        {
            Nombre = "Bebidas Actualizadas",
            Descripcion = "Descripción actualizada",
            EstaActivo = true
        };

        var result = _validator.Validate(dto);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyNombre_ShouldFail()
    {
        var dto = new UpdateCategoriaDto
        {
            Nombre = "",
            Descripcion = "Descripción válida",
            EstaActivo = true
        };

        var result = _validator.Validate(dto);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Nombre" && e.ErrorMessage.Contains("obligatorio"));
    }
}