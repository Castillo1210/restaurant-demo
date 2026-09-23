using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Pedidos.DTOs;
using CaraNegra.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Pedidos.Validators;

public class CreatePedidoDtoValidator : AbstractValidator<CreatePedidoDto>
{
    private readonly IApplicationDbContext _context;

    public CreatePedidoDtoValidator(IApplicationDbContext context)
    {
        _context = context;

        // MustAsync no es compatible con el pipeline de validación automática síncrono de
        // ASP.NET Core — las reglas de abajo usan Must + .Any síncrono en vez de MustAsync/.AnyAsync.
        RuleFor(x => x.MesaId)
            .GreaterThan(0).WithMessage("La mesa es requerida")
            .Must(BeValidMesa).WithMessage("La mesa no existe o no está disponible");

        RuleFor(x => x.UsuarioId)
            .GreaterThan(0).WithMessage("El usuario es requerido")
            .Must(BeValidUsuario).WithMessage("El usuario no existe");

        RuleFor(x => x.Detalles)
            .NotEmpty().WithMessage("El pedido debe tener al menos un item")
            .Must(d => d.Count <= 50).WithMessage("El pedido no puede tener más de 50 items");

        RuleForEach(x => x.Detalles).ChildRules(detalle =>
        {
            detalle.RuleFor(x => x.ProductoId)
                .GreaterThan(0).WithMessage("El producto es requerido")
                .Must(BeValidProducto).WithMessage("El producto no existe o no está disponible");

            detalle.RuleFor(x => x.Cantidad)
                .GreaterThan(0).WithMessage("La cantidad debe ser mayor a 0")
                .LessThanOrEqualTo(100).WithMessage("La cantidad no puede exceder 100");
        });
    }

    private bool BeValidMesa(int mesaId)
    {
        // Una mesa Reservada también puede recibir un pedido nuevo (el cliente de la
        // reserva se sienta y el mozo toma la orden); solo Ocupada/Mantenimiento bloquean.
        return _context.Mesas.Any(m => m.MesaId == mesaId
            && (m.Estado == EstadoMesa.Disponible || m.Estado == EstadoMesa.Reservada));
    }

    private bool BeValidUsuario(int usuarioId)
    {
        return _context.Usuarios.Any(u => u.UsuarioId == usuarioId);
    }

    private bool BeValidProducto(int productoId)
    {
        return _context.Productos.Any(p => p.ProductoId == productoId && p.EstaDisponible);
    }
}