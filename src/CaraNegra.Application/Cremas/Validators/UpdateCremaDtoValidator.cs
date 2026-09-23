using CaraNegra.Application.Cremas.DTOs;
using FluentValidation;

namespace CaraNegra.Application.Cremas.Validators;

public class UpdateCremaDtoValidator : AbstractValidator<UpdateCremaDto>
{
    public UpdateCremaDtoValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(50).WithMessage("El nombre no puede exceder 50 caracteres.");

        RuleFor(x => x.Orden)
            .GreaterThanOrEqualTo(0).WithMessage("El orden no puede ser negativo.");
    }
}
