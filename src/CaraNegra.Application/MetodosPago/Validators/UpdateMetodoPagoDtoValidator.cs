using CaraNegra.Application.MetodosPago.DTOs;
using FluentValidation;

namespace CaraNegra.Application.MetodosPago.Validators;

public class UpdateMetodoPagoDtoValidator : AbstractValidator<UpdateMetodoPagoDto>
{
    public UpdateMetodoPagoDtoValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(50).WithMessage("El nombre no puede exceder 50 caracteres.");
    }
}
