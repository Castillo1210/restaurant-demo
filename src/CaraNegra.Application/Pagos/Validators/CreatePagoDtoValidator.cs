using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Pagos.DTOs;
using CaraNegra.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Pagos.Validators;

public class CreatePagoDtoValidator : AbstractValidator<CreatePagoDto>
{
    private readonly IApplicationDbContext _context;

    public CreatePagoDtoValidator(IApplicationDbContext context)
    {
        _context = context;

        // MustAsync no es compatible con el pipeline de validación automática síncrono de
        // ASP.NET Core (AddFluentValidationAutoValidation lanza
        // AsyncValidatorInvokedSynchronouslyException si detecta reglas asíncronas) — las tres
        // reglas de abajo usan Must + consultas EF Core síncronas (.Any/.FirstOrDefault) en vez
        // de MustAsync + sus equivalentes asíncronos.
        RuleFor(x => x.PedidoId)
            .GreaterThan(0).WithMessage("El pedido es requerido")
            .Must(BeValidPedidoParaPago).WithMessage("El pedido no existe, ya está pagado completamente o está cancelado");

        RuleFor(x => x.Monto)
            .GreaterThan(0).WithMessage("El monto debe ser mayor a 0")
            .Must(BeMontoValido).WithMessage("El monto excede el saldo pendiente del pedido");

        RuleFor(x => x.MetodoPagoId)
            .GreaterThan(0).WithMessage("El método de pago es requerido")
            .Must(BeValidMetodoPago).WithMessage("El método de pago no existe o no está activo");

        RuleFor(x => x.Referencia)
            .MaximumLength(100).WithMessage("La referencia no puede exceder 100 caracteres");
    }

    private bool BeValidPedidoParaPago(int pedidoId)
    {
        return _context.Pedidos
            .Any(p => p.PedidoId == pedidoId
                && p.EstadoPedido != EstadoPedido.Cancelado
                && p.EstadoPedido != EstadoPedido.Entregado);
    }

    private bool BeMontoValido(CreatePagoDto dto, decimal monto)
    {
        var pedido = _context.Pedidos
            .Include(p => p.Pagos)
            .FirstOrDefault(p => p.PedidoId == dto.PedidoId);

        if (pedido == null) return false;

        var pagado = pedido.Pagos.Where(p => !p.EstaAnulado).Sum(p => p.Monto);
        var saldoPendiente = pedido.Total - pagado;

        return monto <= saldoPendiente + 0.01m; // Tolerancia decimal
    }

    private bool BeValidMetodoPago(int metodoPagoId)
    {
        return _context.MetodosPago.Any(m => m.MetodoPagoId == metodoPagoId && m.EstaActivo);
    }
}