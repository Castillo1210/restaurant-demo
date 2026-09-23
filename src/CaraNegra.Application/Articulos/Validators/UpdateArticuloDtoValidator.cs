using CaraNegra.Application.Articulos.DTOs;
using CaraNegra.Application.Common.Interfaces;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Articulos.Validators;

public class UpdateArticuloDtoValidator : AbstractValidator<UpdateArticuloDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateArticuloDtoValidator(IApplicationDbContext context)
    {
        _context = context;

        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(150).WithMessage("El nombre no puede exceder 150 caracteres.");

        RuleFor(x => x.Descripcion)
            .MaximumLength(1000).WithMessage("La descripción no puede exceder 1000 caracteres.");

        RuleFor(x => x.Precio)
            .GreaterThanOrEqualTo(0).WithMessage("El precio no puede ser negativo.")
            .LessThanOrEqualTo(999999.99m).WithMessage("El precio no puede exceder 999,999.99.");

        RuleFor(x => x.Tipo)
            .NotEmpty().WithMessage("El tipo es obligatorio.")
            .MaximumLength(50).WithMessage("El tipo no puede exceder 50 caracteres.");

        RuleFor(x => x.CategoriaId)
            .GreaterThan(0).WithMessage("La categoría es obligatoria.")
            // Ver el comentario en CreateArticuloDtoValidator: MustAsync no es compatible con
            // el pipeline de validación automática síncrono de ASP.NET Core.
            .Must(BeValidCategoriaActiva).WithMessage("La categoría no existe o no está activa.");
    }

    private bool BeValidCategoriaActiva(int categoriaId)
    {
        return _context.Categorias.Any(c => c.CategoriaId == categoriaId && c.EstaActivo);
    }
}
