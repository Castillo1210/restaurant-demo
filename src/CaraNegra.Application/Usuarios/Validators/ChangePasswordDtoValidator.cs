using CaraNegra.Application.Usuarios.DTOs;
using FluentValidation;

namespace CaraNegra.Application.Usuarios.Validators;

public class ChangePasswordDtoValidator : AbstractValidator<ChangePasswordDto>
{
    public ChangePasswordDtoValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("La contraseña actual es requerida");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("La nueva contraseña es requerida")
            .MinimumLength(8).WithMessage("La nueva contraseña debe tener al menos 8 caracteres")
            .MaximumLength(100).WithMessage("La nueva contraseña no puede exceder 100 caracteres")
            .Matches("[A-Z]").WithMessage("La nueva contraseña debe contener al menos una mayúscula")
            .Matches("[a-z]").WithMessage("La nueva contraseña debe contener al menos una minúscula")
            .Matches("[0-9]").WithMessage("La nueva contraseña debe contener al menos un número")
            .Matches("[^a-zA-Z0-9]").WithMessage("La nueva contraseña debe contener al menos un carácter especial")
            .NotEqual(x => x.CurrentPassword).WithMessage("La nueva contraseña debe ser diferente a la actual");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage("La confirmación de contraseña es requerida")
            .Equal(x => x.NewPassword).WithMessage("Las contraseñas no coinciden");
    }
}