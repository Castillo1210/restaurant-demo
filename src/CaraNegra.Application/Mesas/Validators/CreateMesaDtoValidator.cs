using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Mesas.DTOs;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Mesas.Validators;

public class CreateMesaDtoValidator : AbstractValidator<CreateMesaDto>
{
    private readonly IApplicationDbContext _context;

    public CreateMesaDtoValidator(IApplicationDbContext context)
    {
        _context = context;

        RuleFor(x => x.NumeroMesa)
            .NotEmpty().WithMessage("El número/código de mesa es requerido")
            .MaximumLength(20).WithMessage("El número/código de mesa no puede tener más de 20 caracteres")
            // MustAsync no es compatible con el pipeline de validación automática síncrono de
            // ASP.NET Core (AddFluentValidationAutoValidation) — se usa Must + .Any síncrono.
            .Must(BeUniqueNumeroMesa).WithMessage("El número/código de mesa ya existe");
    }

    private bool BeUniqueNumeroMesa(string numeroMesa)
    {
        return !_context.Mesas.Any(m => m.NumeroMesa == numeroMesa);
    }
}