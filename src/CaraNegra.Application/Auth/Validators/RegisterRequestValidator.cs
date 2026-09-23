using CaraNegra.Application.Auth.DTOs;
using FluentValidation;

namespace CaraNegra.Application.Auth.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.NombreUsuario)
            .NotEmpty().WithMessage("El nombre de usuario es obligatorio.")
            .MinimumLength(3).WithMessage("El nombre de usuario debe tener al menos 3 caracteres.")
            .MaximumLength(50).WithMessage("El nombre de usuario no puede exceder 50 caracteres.")
            .Matches("^[a-zA-Z0-9_]+$").WithMessage("El nombre de usuario solo puede contener letras, números y guiones bajos.");

        RuleFor(x => x.NombreCompleto)
            .NotEmpty().WithMessage("El nombre completo es obligatorio.")
            .MaximumLength(100).WithMessage("El nombre completo no puede exceder 100 caracteres.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MinimumLength(8).WithMessage("La contraseña debe tener al menos 8 caracteres.")
            .MaximumLength(100).WithMessage("La contraseña no puede exceder 100 caracteres.")
            .Matches("[A-Z]").WithMessage("La contraseña debe contener al menos una mayúscula.")
            .Matches("[a-z]").WithMessage("La contraseña debe contener al menos una minúscula.")
            .Matches("[0-9]").WithMessage("La contraseña debe contener al menos un número.")
            .Matches("[^a-zA-Z0-9]").WithMessage("La contraseña debe contener al menos un carácter especial.");

        RuleFor(x => x.RolId)
            .GreaterThan(0).WithMessage("El rol es obligatorio.");
    }
}