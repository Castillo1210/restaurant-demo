using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Pedidos.DTOs;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Pedidos.Validators;

public class UpdatePedidoDtoValidator : AbstractValidator<UpdatePedidoDto>
{
    private readonly IApplicationDbContext _context;

    public UpdatePedidoDtoValidator(IApplicationDbContext context)
    {
        _context = context;

        // MustAsync no es compatible con el pipeline de validación automática síncrono de
        // ASP.NET Core — se usa Must + .Any síncrono (ver CreatePedidoDtoValidator).
        RuleFor(x => x.MesaId)
            .GreaterThan(0).WithMessage("La mesa es requerida")
            .Must(BeValidMesa).WithMessage("La mesa no existe");

        RuleFor(x => x.UsuarioId)
            .GreaterThan(0).WithMessage("El usuario es requerido")
            .Must(BeValidUsuario).WithMessage("El usuario no existe");
    }

    private bool BeValidMesa(int mesaId)
    {
        return _context.Mesas.Any(m => m.MesaId == mesaId);
    }

    private bool BeValidUsuario(int usuarioId)
    {
        return _context.Usuarios.Any(u => u.UsuarioId == usuarioId);
    }
}