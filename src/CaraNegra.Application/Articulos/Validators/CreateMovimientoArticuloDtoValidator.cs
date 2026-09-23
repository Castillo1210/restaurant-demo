using CaraNegra.Application.Articulos.DTOs;
using FluentValidation;

namespace CaraNegra.Application.Articulos.Validators;

public class CreateMovimientoArticuloDtoValidator : AbstractValidator<CreateMovimientoArticuloDto>
{
    private static readonly string[] TiposValidos = ["Entrada", "Salida", "Ajuste"];

    public CreateMovimientoArticuloDtoValidator()
    {
        RuleFor(x => x.TipoMovimiento)
            .NotEmpty().WithMessage("El tipo de movimiento es obligatorio.")
            .Must(t => TiposValidos.Contains(t)).WithMessage("El tipo de movimiento debe ser Entrada, Salida o Ajuste.");

        RuleFor(x => x.Cantidad)
            .GreaterThanOrEqualTo(0).WithMessage("La cantidad no puede ser negativa.");

        RuleFor(x => x.ReferenciaCod)
            .MaximumLength(100).WithMessage("La referencia no puede exceder 100 caracteres.");

        RuleFor(x => x.Notas)
            .MaximumLength(500).WithMessage("Las notas no pueden exceder 500 caracteres.");
    }
}
