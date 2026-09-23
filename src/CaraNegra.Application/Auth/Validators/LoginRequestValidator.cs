using CaraNegra.Application.Auth.DTOs;
using FluentValidation;

namespace CaraNegra.Application.Auth.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.NombreUsuario)
            .NotEmpty().WithMessage("El nombre de usuario es obligatorio.")
            .MaximumLength(50).WithMessage("El nombre de usuario no puede exceder 50 caracteres.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MinimumLength(6).WithMessage("La contraseña debe tener al menos 6 caracteres.")
            .MaximumLength(100).WithMessage("La contraseña no puede exceder 100 caracteres.");
    }
}