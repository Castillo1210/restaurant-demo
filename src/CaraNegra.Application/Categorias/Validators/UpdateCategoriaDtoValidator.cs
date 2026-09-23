using CaraNegra.Application.Categorias.DTOs;
using FluentValidation;

namespace CaraNegra.Application.Categorias.Validators;

public class UpdateCategoriaDtoValidator : AbstractValidator<UpdateCategoriaDto>
{
    public UpdateCategoriaDtoValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(100).WithMessage("El nombre no puede exceder 100 caracteres.");

        RuleFor(x => x.Descripcion)
            .MaximumLength(500).WithMessage("La descripción no puede exceder 500 caracteres.");
    }
}