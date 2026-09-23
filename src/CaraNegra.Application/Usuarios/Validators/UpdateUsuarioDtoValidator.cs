using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Usuarios.DTOs;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Usuarios.Validators;

public class UpdateUsuarioDtoValidator : AbstractValidator<UpdateUsuarioDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateUsuarioDtoValidator(IApplicationDbContext context)
    {
        _context = context;

        RuleFor(x => x.NombreCompleto)
            .NotEmpty().WithMessage("El nombre completo es obligatorio")
            .MaximumLength(100).WithMessage("El nombre completo no puede exceder 100 caracteres");

        RuleFor(x => x.RolId)
            .GreaterThan(0).WithMessage("El rol es obligatorio")
            // MustAsync no es compatible con el pipeline de validación automática síncrono de
            // ASP.NET Core — se usa Must + .Any síncrono (ver CreateUsuarioDtoValidator).
            .Must(BeValidRol).WithMessage("El rol no existe");
    }

    private bool BeValidRol(int rolId)
    {
        return _context.Roles.Any(r => r.RolId == rolId);
    }
}