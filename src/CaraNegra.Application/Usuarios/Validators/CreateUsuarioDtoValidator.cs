using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Usuarios.DTOs;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Usuarios.Validators;

public class CreateUsuarioDtoValidator : AbstractValidator<CreateUsuarioDto>
{
    private readonly IApplicationDbContext _context;

    public CreateUsuarioDtoValidator(IApplicationDbContext context)
    {
        _context = context;

        RuleFor(x => x.NombreUsuario)
            .NotEmpty().WithMessage("El nombre de usuario es obligatorio")
            .MinimumLength(3).WithMessage("El nombre de usuario debe tener al menos 3 caracteres")
            .MaximumLength(50).WithMessage("El nombre de usuario no puede exceder 50 caracteres")
            .Matches("^[a-zA-Z0-9_]+$").WithMessage("El nombre de usuario solo puede contener letras, números y guiones bajos")
            // MustAsync no es compatible con el pipeline de validación automática síncrono de
            // ASP.NET Core (AddFluentValidationAutoValidation lanza
            // AsyncValidatorInvokedSynchronouslyException) — se usa Must + .Any síncrono.
            .Must(BeUniqueNombreUsuario).WithMessage("El nombre de usuario ya existe");

        RuleFor(x => x.NombreCompleto)
            .NotEmpty().WithMessage("El nombre completo es obligatorio")
            .MaximumLength(100).WithMessage("El nombre completo no puede exceder 100 caracteres");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contraseña es obligatoria")
            .MinimumLength(8).WithMessage("La contraseña debe tener al menos 8 caracteres")
            .MaximumLength(100).WithMessage("La contraseña no puede exceder 100 caracteres")
            .Matches("[A-Z]").WithMessage("La contraseña debe contener al menos una mayúscula")
            .Matches("[a-z]").WithMessage("La contraseña debe contener al menos una minúscula")
            .Matches("[0-9]").WithMessage("La contraseña debe contener al menos un número")
            .Matches("[^a-zA-Z0-9]").WithMessage("La contraseña debe contener al menos un carácter especial");

        RuleFor(x => x.RolId)
            .GreaterThan(0).WithMessage("El rol es obligatorio")
            .Must(BeValidRol).WithMessage("El rol no existe");
    }

    private bool BeUniqueNombreUsuario(string nombreUsuario)
    {
        return !_context.Usuarios.Any(u => u.NombreUsuario == nombreUsuario);
    }

    private bool BeValidRol(int rolId)
    {
        return _context.Roles.Any(r => r.RolId == rolId);
    }
}