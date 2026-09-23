using CaraNegra.Application.Descuentos.DTOs;
using FluentValidation;

namespace CaraNegra.Application.Descuentos.Validators;

public class CreateDescuentoDtoValidator : AbstractValidator<CreateDescuentoDto>
{
    public CreateDescuentoDtoValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre del descuento es requerido")
            .MaximumLength(100).WithMessage("El nombre no puede tener más de 100 caracteres");

        RuleFor(x => x.Valor)
            .GreaterThan(0).WithMessage("El valor del descuento debe ser mayor a 0");

        RuleFor(x => x.Valor)
            .LessThanOrEqualTo(100).WithMessage("Un descuento porcentual no puede ser mayor a 100%")
            .When(x => x.EsPorcentaje);

        RuleFor(x => x.FechaFin)
            .GreaterThanOrEqualTo(x => x.FechaInicio!.Value)
            .WithMessage("La fecha de fin no puede ser anterior a la fecha de inicio")
            .When(x => x.FechaInicio.HasValue && x.FechaFin.HasValue);
    }
}
