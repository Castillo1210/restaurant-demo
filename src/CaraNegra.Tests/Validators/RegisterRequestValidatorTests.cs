using CaraNegra.Application.Auth.DTOs;
using CaraNegra.Application.Auth.Validators;
using FluentAssertions;
using Xunit;

namespace CaraNegra.Tests.Validators;

public class RegisterRequestValidatorTests
{
    private readonly RegisterRequestValidator _validator = new();

    [Fact]
    public void Validate_ValidRequest_ShouldPass()
    {
        var request = new RegisterRequest
        {
            NombreUsuario = "usuario123",
            NombreCompleto = "Usuario Prueba",
            Password = "Password123!",
            RolId = 1
        };

        var result = _validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShortUsername_ShouldFail()
    {
        var request = new RegisterRequest
        {
            NombreUsuario = "ab",
            NombreCompleto = "Usuario Prueba",
            Password = "Password123!",
            RolId = 1
        };

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "NombreUsuario" && e.ErrorMessage.Contains("3 caracteres"));
    }

    [Fact]
    public void Validate_InvalidUsernameCharacters_ShouldFail()
    {
        var request = new RegisterRequest
        {
            NombreUsuario = "usuario@123",
            NombreCompleto = "Usuario Prueba",
            Password = "Password123!",
            RolId = 1
        };

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "NombreUsuario" && e.ErrorMessage.Contains("letras, números y guiones bajos"));
    }

    [Fact]
    public void Validate_WeakPassword_ShouldFail()
    {
        var request = new RegisterRequest
        {
            NombreUsuario = "usuario123",
            NombreCompleto = "Usuario Prueba",
            Password = "password",
            RolId = 1
        };

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password" && e.ErrorMessage.Contains("mayúscula"));
        result.Errors.Should().Contain(e => e.PropertyName == "Password" && e.ErrorMessage.Contains("número"));
        result.Errors.Should().Contain(e => e.PropertyName == "Password" && e.ErrorMessage.Contains("carácter especial"));
    }

    [Fact]
    public void Validate_RolIdZero_ShouldFail()
    {
        var request = new RegisterRequest
        {
            NombreUsuario = "usuario123",
            NombreCompleto = "Usuario Prueba",
            Password = "Password123!",
            RolId = 0
        };

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "RolId" && e.ErrorMessage.Contains("obligatorio"));
    }
}