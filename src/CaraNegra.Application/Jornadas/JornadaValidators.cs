using FluentValidation;

namespace CaraNegra.Application.Jornadas;

public class AbrirJornadaDtoValidator : AbstractValidator<AbrirJornadaDto>
{
    public AbrirJornadaDtoValidator()
    {
        RuleFor(x => x.FondoInicial)
            .GreaterThanOrEqualTo(0).WithMessage("El fondo de caja no puede ser negativo")
            .LessThanOrEqualTo(100000).WithMessage("El fondo de caja es demasiado alto");
        RuleFor(x => x.Observacion).MaximumLength(300).WithMessage("La observación no puede exceder 300 caracteres");
        RuleFor(x => x.FechaOperativa)
            .Must(f => f is null || (f.Value.Date <= JornadaReglas.HoyLima() && f.Value.Date >= JornadaReglas.HoyLima().AddDays(-7)))
            .WithMessage("La fecha de la jornada debe ser de hoy o de los últimos 7 días");
    }
}

public class RegistrarMovimientoDtoValidator : AbstractValidator<RegistrarMovimientoDto>
{
    public RegistrarMovimientoDtoValidator()
    {
        RuleFor(x => x.Tipo).IsInEnum().WithMessage("Tipo de movimiento inválido");
        RuleFor(x => x.Monto)
            .GreaterThan(0).WithMessage("El monto debe ser mayor a 0")
            .LessThanOrEqualTo(100000).WithMessage("El monto es demasiado alto");
        RuleFor(x => x.Motivo)
            .NotEmpty().WithMessage("Indica el motivo (p. ej. \"compra de gas\")")
            .MaximumLength(200).WithMessage("El motivo no puede exceder 200 caracteres");
    }
}

public class CerrarJornadaDtoValidator : AbstractValidator<CerrarJornadaDto>
{
    public CerrarJornadaDtoValidator()
    {
        // Conteos: ya no se usan (el cierre es con lo registrado); se aceptan por compatibilidad.
        RuleForEach(x => x.Conteos).ChildRules(c =>
            c.RuleFor(x => x.Contado)
                .GreaterThanOrEqualTo(0).WithMessage("Lo contado no puede ser negativo")
                .LessThanOrEqualTo(10000000).WithMessage("Monto contado inválido"));
        RuleFor(x => x.Observacion).MaximumLength(500).WithMessage("La observación no puede exceder 500 caracteres");
    }
}
