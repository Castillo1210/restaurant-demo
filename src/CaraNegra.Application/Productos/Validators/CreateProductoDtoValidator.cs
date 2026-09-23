using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Productos.DTOs;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Productos.Validators;

public class CreateProductoDtoValidator : AbstractValidator<CreateProductoDto>
{
    private readonly IApplicationDbContext _context;

    public CreateProductoDtoValidator(IApplicationDbContext context)
    {
        _context = context;

        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(150).WithMessage("El nombre no puede exceder 150 caracteres.");

        RuleFor(x => x.Descripcion)
            .MaximumLength(1000).WithMessage("La descripción no puede exceder 1000 caracteres.");

        RuleFor(x => x.Precio)
            .GreaterThan(0).WithMessage("El precio debe ser mayor a 0.")
            .LessThanOrEqualTo(999999.99m).WithMessage("El precio no puede exceder 999,999.99.");

        RuleFor(x => x.Tipo)
            .NotEmpty().WithMessage("El tipo es obligatorio.")
            .MaximumLength(50).WithMessage("El tipo no puede exceder 50 caracteres.");

        RuleFor(x => x.CategoriaId)
            .GreaterThan(0).WithMessage("La categoría es obligatoria.")
            // Antes de esta validación, crear un producto con una CategoriaId inexistente
            // no fallaba con un 400 claro, sino con una DbUpdateException (500) al violar
            // la clave foránea — el mensaje ahora es explícito y ocurre antes de tocar la BD.
            // MustAsync no es compatible con el pipeline de validación automática síncrono de
            // ASP.NET Core (AddFluentValidationAutoValidation lanza
            // AsyncValidatorInvokedSynchronouslyException) — se usa Must + .Any síncrono.
            .Must(BeValidCategoriaActiva).WithMessage("La categoría no existe o no está activa.");
    }

    private bool BeValidCategoriaActiva(int categoriaId)
    {
        return _context.Categorias.Any(c => c.CategoriaId == categoriaId && c.EstaActivo);
    }
}