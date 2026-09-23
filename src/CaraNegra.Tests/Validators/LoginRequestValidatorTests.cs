using CaraNegra.Application.Auth.DTOs;
using CaraNegra.Application.Auth.Validators;
using FluentAssertions;
using Xunit;

namespace CaraNegra.Tests.Validators;

public class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _validator = new();

    [Fact]
    public void Validate_ValidRequest_ShouldPass()
    {
        var request = new LoginRequest
        {
            NombreUsuario = "usuario123",
            Password = "password123"
        };

        var result = _validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyUsername_ShouldFail()
    {
        var request = new LoginRequest
        {
            NombreUsuario = "",
            Password = "password123"
        };

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "NombreUsuario" && e.ErrorMessage.Contains("obligatorio"));
    }

    [Fact]
    public void Validate_ShortPassword_ShouldFail()
    {
        var request = new LoginRequest
        {
            NombreUsuario = "usuario123",
            Password = "123"
        };

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password" && e.ErrorMessage.Contains("6 caracteres"));
    }
}