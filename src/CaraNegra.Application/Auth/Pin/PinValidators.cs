using FluentValidation;

namespace CaraNegra.Application.Auth.Pin;

public class AsignarPinDtoValidator : AbstractValidator<AsignarPinDto>
{
    public AsignarPinDtoValidator()
    {
        RuleFor(x => x.Pin)
            .Must(PinReglas.TieneFormatoValido).WithMessage("El PIN debe tener exactamente 4 dígitos")
            .Must(p => !PinReglas.EsTrivial(p)).WithMessage("PIN muy fácil de adivinar (evita 0000, 1111, 1234, 4321…)");
    }
}

public class LoginPinRequestValidator : AbstractValidator<LoginPinRequest>
{
    public LoginPinRequestValidator()
    {
        RuleFor(x => x.UsuarioId).GreaterThan(0).WithMessage("Selecciona tu nombre");
        RuleFor(x => x.Pin).Must(PinReglas.TieneFormatoValido).WithMessage("El PIN debe tener 4 dígitos");
    }
}

public class RegistrarDispositivoDtoValidator : AbstractValidator<RegistrarDispositivoDto>
{
    public RegistrarDispositivoDtoValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("Ponle un nombre al dispositivo (ej. \"Tablet terraza\")")
            .MaximumLength(60).WithMessage("El nombre no puede exceder 60 caracteres");
        RuleFor(x => x.CierreInactividadMinutos)
            .InclusiveBetween(0, 240).WithMessage("El cierre por inactividad debe estar entre 0 (nunca) y 240 minutos");
    }
}
