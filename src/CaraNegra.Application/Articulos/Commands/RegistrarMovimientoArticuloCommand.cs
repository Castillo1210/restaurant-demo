using CaraNegra.Application.Articulos.DTOs;
using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Domain.Entities;
using CaraNegra.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Articulos.Commands;

public record RegistrarMovimientoArticuloCommand(int ArticuloId, int UsuarioId, CreateMovimientoArticuloDto Dto)
    : IRequest<MovimientoArticuloDto>;

public class RegistrarMovimientoArticuloCommandHandler : IRequestHandler<RegistrarMovimientoArticuloCommand, MovimientoArticuloDto>
{
    private readonly IApplicationDbContext _context;

    public RegistrarMovimientoArticuloCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<MovimientoArticuloDto> Handle(RegistrarMovimientoArticuloCommand request, CancellationToken cancellationToken)
    {
        var articulo = await _context.Articulos
            .FirstOrDefaultAsync(a => a.ArticuloId == request.ArticuloId, cancellationToken)
            ?? throw new KeyNotFoundException($"Artículo {request.ArticuloId} no encontrado.");

        if (!Enum.TryParse<TipoMovimiento>(request.Dto.TipoMovimiento, out var tipo))
        {
            throw new InvalidOperationException("El tipo de movimiento debe ser Entrada, Salida o Ajuste.");
        }

        int nuevoStock;
        switch (tipo)
        {
            case TipoMovimiento.Entrada:
                nuevoStock = articulo.Stock + request.Dto.Cantidad;
                break;

            case TipoMovimiento.Salida:
                if (request.Dto.Cantidad > articulo.Stock)
                {
                    throw new InvalidOperationException(
                        $"No hay stock suficiente de \"{articulo.Nombre}\" (stock actual: {articulo.Stock}, solicitado: {request.Dto.Cantidad}).");
                }
                nuevoStock = articulo.Stock - request.Dto.Cantidad;
                break;

            case TipoMovimiento.Ajuste:
                // El ajuste corrige el stock a un valor exacto (p.ej. tras un conteo físico),
                // sin importar el valor anterior — no es un delta como Entrada/Salida.
                nuevoStock = request.Dto.Cantidad;
                break;

            default:
                throw new InvalidOperationException("Tipo de movimiento no soportado.");
        }

        articulo.Stock = nuevoStock;

        var movimiento = new MovimientoArticulo
        {
            ArticuloId = articulo.ArticuloId,
            TipoMovimiento = tipo,
            Cantidad = request.Dto.Cantidad,
            Balance = nuevoStock,
            ReferenciaCod = request.Dto.ReferenciaCod,
            Notas = request.Dto.Notas,
            UsuarioId = request.UsuarioId
        };

        _context.MovimientosArticulo.Add(movimiento);
        await _context.SaveChangesAsync(cancellationToken);

        var usuario = await _context.Usuarios.FindAsync(new object[] { request.UsuarioId }, cancellationToken);

        return new MovimientoArticuloDto
        {
            MovimientoArticuloId = movimiento.MovimientoArticuloId,
            ArticuloId = articulo.ArticuloId,
            ArticuloNombre = articulo.Nombre,
            TipoMovimiento = movimiento.TipoMovimiento.ToString(),
            Cantidad = movimiento.Cantidad,
            Balance = movimiento.Balance,
            ReferenciaCod = movimiento.ReferenciaCod,
            Notas = movimiento.Notas,
            UsuarioId = movimiento.UsuarioId,
            UsuarioNombre = usuario?.NombreCompleto ?? string.Empty,
            CreadoEn = movimiento.CreadoEn
        };
    }
}
