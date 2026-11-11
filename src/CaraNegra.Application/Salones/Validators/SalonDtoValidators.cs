using CaraNegra.Application.Salones.DTOs;
using FluentValidation;

namespace CaraNegra.Application.Salones.Validators;

// La unicidad del nombre se valida en los handlers (necesitan el SalonId de la ruta para
// excluir el propio salón al editar), igual que se hace con el número de mesa.

public class CreateSalonDtoValidator : AbstractValidator<CreateSalonDto>
{
    public CreateSalonDtoValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre del salón es requerido")
            .MaximumLength(50).WithMessage("El nombre del salón no puede tener más de 50 caracteres");
        RuleFor(x => x.Descripcion)
            .MaximumLength(200).WithMessage("La descripción no puede tener más de 200 caracteres");
        RuleFor(x => x.Orden)
            .GreaterThanOrEqualTo(0).WithMessage("El orden no puede ser negativo");
    }
}

public class UpdateSalonDtoValidator : AbstractValidator<UpdateSalonDto>
{
    public UpdateSalonDtoValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre del salón es requerido")
            .MaximumLength(50).WithMessage("El nombre del salón no puede tener más de 50 caracteres");
        RuleFor(x => x.Descripcion)
            .MaximumLength(200).WithMessage("La descripción no puede tener más de 200 caracteres");
        RuleFor(x => x.Orden)
            .GreaterThanOrEqualTo(0).WithMessage("El orden no puede ser negativo");
    }
}
