using CaraNegra.Application.Common;
using CaraNegra.Application.Empresa.DTOs;
using FluentValidation;

namespace CaraNegra.Application.Empresa.Validators;

public class UpdateConfiguracionEmpresaDtoValidator : AbstractValidator<UpdateConfiguracionEmpresaDto>
{
    // ~300 KB de imagen en base64: de sobra para un logo reducido a 320 px.
    public const int MaxLargoLogo = 400_000;

    public UpdateConfiguracionEmpresaDtoValidator()
    {
        RuleFor(x => x.Ruc)
            .Must(DocumentoIdentidadHelper.EsRucValido)
            .WithMessage("El RUC no es válido (11 dígitos y dígito verificador correcto)");
        RuleFor(x => x.RazonSocial)
            .NotEmpty().WithMessage("La razón social es obligatoria")
            .MaximumLength(200).WithMessage("La razón social no puede exceder 200 caracteres");
        RuleFor(x => x.NombreComercial)
            .NotEmpty().WithMessage("El nombre comercial es obligatorio")
            .MaximumLength(100).WithMessage("El nombre comercial no puede exceder 100 caracteres");
        RuleFor(x => x.Direccion)
            .NotEmpty().WithMessage("La dirección fiscal es obligatoria")
            .MaximumLength(250).WithMessage("La dirección no puede exceder 250 caracteres");
        RuleFor(x => x.Telefono)
            .MaximumLength(30).WithMessage("El teléfono no puede exceder 30 caracteres");
        RuleFor(x => x.Email)
            .MaximumLength(100).WithMessage("El correo no puede exceder 100 caracteres")
            .Matches(@"^[^@\s]+@[^@\s]+\.[^@\s]+$").When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("El correo no es válido");

        // SUNAT: series de 4 caracteres; boletas empiezan con B y facturas con F.
        RuleFor(x => x.SerieBoleta)
            .Matches("^B[A-Z0-9]{3}$").WithMessage("La serie de boletas debe tener 4 caracteres y empezar con B (ej. B001)");
        RuleFor(x => x.SerieFactura)
            .Matches("^F[A-Z0-9]{3}$").WithMessage("La serie de facturas debe tener 4 caracteres y empezar con F (ej. F001)");

        RuleFor(x => x.IgvPorcentaje)
            .GreaterThanOrEqualTo(0).WithMessage("El IGV no puede ser negativo")
            .LessThanOrEqualTo(100).WithMessage("El IGV no puede ser mayor a 100%");

        RuleFor(x => x.MensajePie)
            .MaximumLength(200).WithMessage("El mensaje al pie no puede exceder 200 caracteres");

        RuleFor(x => x.LogoBase64)
            .Must(l => string.IsNullOrEmpty(l) || l.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            .WithMessage("El logo debe ser una imagen")
            .MaximumLength(MaxLargoLogo).WithMessage("El logo es demasiado grande; use una imagen más pequeña");
    }
}
