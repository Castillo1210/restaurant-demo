using CaraNegra.Application.Pedidos.DTOs;
using CaraNegra.Domain.Enums;
using FluentValidation;

namespace CaraNegra.Application.Pedidos.Validators;

public class UpdatePedidoEstadoDtoValidator : AbstractValidator<UpdatePedidoEstadoDto>
{
    public UpdatePedidoEstadoDtoValidator()
    {
        RuleFor(x => x.EstadoPedido)
            .IsInEnum().WithMessage("El estado del pedido no es válido")
            .Must(BeValidTransition).WithMessage("La transición de estado no es válida");
    }

    private bool BeValidTransition(EstadoPedido nuevoEstado)
    {
        // Las transiciones válidas se validan en el handler con el estado actual
        // Aquí solo validamos que sea un valor de enum válido
        return Enum.IsDefined(typeof(EstadoPedido), nuevoEstado);
    }
}