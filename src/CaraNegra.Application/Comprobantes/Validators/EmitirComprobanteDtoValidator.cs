using CaraNegra.Application.Common;
using CaraNegra.Application.Comprobantes.DTOs;
using CaraNegra.Domain.Enums;
using FluentValidation;

namespace CaraNegra.Application.Comprobantes.Validators;

public class EmitirComprobanteDtoValidator : AbstractValidator<EmitirComprobanteDto>
{
    public EmitirComprobanteDtoValidator()
    {
        RuleFor(x => x.PedidoId).GreaterThan(0).WithMessage("Debe indicar el pedido");
        RuleFor(x => x.TipoComprobante).IsInEnum();
        RuleFor(x => x.ClienteTipoDocumento).IsInEnum();

        RuleFor(x => x.ClienteNombre).MaximumLength(150);
        RuleFor(x => x.ClienteDireccion).MaximumLength(200);

        // Factura: SUNAT exige RUC válido, razón social y dirección fiscal del adquiriente.
        When(x => x.TipoComprobante == TipoComprobante.Factura, () =>
        {
            RuleFor(x => x.ClienteTipoDocumento)
                .Equal(TipoDocumentoIdentidad.RUC)
                .WithMessage("La factura solo puede emitirse a un cliente con RUC");
            RuleFor(x => x.ClienteNumeroDocumento)
                .Must(DocumentoIdentidadHelper.EsRucValido)
                .WithMessage("El RUC no es válido (11 dígitos y dígito verificador correcto)");
            RuleFor(x => x.ClienteNombre)
                .NotEmpty().WithMessage("La razón social es obligatoria para emitir factura");
        });

        // Boleta: el documento es opcional, pero si se indica debe tener un formato válido.
        When(x => x.TipoComprobante == TipoComprobante.Boleta, () =>
        {
            RuleFor(x => x.ClienteTipoDocumento)
                .NotEqual(TipoDocumentoIdentidad.RUC)
                .WithMessage("Para un cliente con RUC emita una factura");
            RuleFor(x => x.ClienteNumeroDocumento)
                .Must(DocumentoIdentidadHelper.EsDniValido)
                .When(x => x.ClienteTipoDocumento == TipoDocumentoIdentidad.DNI)
                .WithMessage("El DNI debe tener 8 dígitos");
            RuleFor(x => x.ClienteNumeroDocumento)
                .Must(DocumentoIdentidadHelper.EsCarnetExtranjeriaValido)
                .When(x => x.ClienteTipoDocumento == TipoDocumentoIdentidad.CarnetExtranjeria)
                .WithMessage("El carné de extranjería no es válido");
            RuleFor(x => x.ClienteNombre)
                .NotEmpty()
                .When(x => x.ClienteTipoDocumento != TipoDocumentoIdentidad.SinDocumento)
                .WithMessage("Indique el nombre del cliente");
        });
    }
}
