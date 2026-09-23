using CaraNegra.Application.Mesas.DTOs;
using FluentValidation;

namespace CaraNegra.Application.Mesas.Validators;

public class UpdateMesaDtoValidator : AbstractValidator<UpdateMesaDto>
{
    public UpdateMesaDtoValidator()
    {
        // La unicidad del número de mesa se valida en UpdateMesaCommandHandler, no aquí:
        // este validador solo recibe el DTO (sin el MesaId de la ruta), así que no puede
        // excluir la propia mesa que se está editando de la comprobación de unicidad
        // (ese era el bug original: editar una mesa sin cambiar su número siempre fallaba).
        RuleFor(x => x.NumeroMesa)
            .NotEmpty().WithMessage("El número/código de mesa es requerido")
            .MaximumLength(20).WithMessage("El número/código de mesa no puede tener más de 20 caracteres");

        RuleFor(x => x.Estado)
            .IsInEnum().WithMessage("El estado de la mesa no es válido");
    }
}