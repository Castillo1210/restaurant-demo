using CaraNegra.Application.Mesas.DTOs;
using FluentValidation;

namespace CaraNegra.Application.Mesas.Validators;

public class CreateMesaDtoValidator : AbstractValidator<CreateMesaDto>
{
    public CreateMesaDtoValidator()
    {
        // La unicidad (número de mesa dentro del salón) y la existencia del salón se validan en
        // CreateMesaCommandHandler: la numeración ahora es por salón, no global.
        RuleFor(x => x.NumeroMesa)
            .NotEmpty().WithMessage("El número/código de mesa es requerido")
            .MaximumLength(20).WithMessage("El número/código de mesa no puede tener más de 20 caracteres");

        RuleFor(x => x.SalonId)
            .GreaterThan(0).WithMessage("Seleccione el salón al que pertenece la mesa");
    }
}
