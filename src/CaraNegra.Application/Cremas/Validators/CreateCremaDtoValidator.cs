using CaraNegra.Application.Cremas.DTOs;
using FluentValidation;

namespace CaraNegra.Application.Cremas.Validators;

public class CreateCremaDtoValidator : AbstractValidator<CreateCremaDto>
{
    public CreateCremaDtoValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(50).WithMessage("El nombre no puede exceder 50 caracteres.");
    }
}
