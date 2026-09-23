using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.MetodosPago.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.MetodosPago.Commands;

public record UpdateMetodoPagoCommand(int MetodoPagoId, UpdateMetodoPagoDto Dto) : IRequest<MetodoPagoDto>;

public class UpdateMetodoPagoCommandHandler : IRequestHandler<UpdateMetodoPagoCommand, MetodoPagoDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateMetodoPagoCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<MetodoPagoDto> Handle(UpdateMetodoPagoCommand request, CancellationToken cancellationToken)
    {
        var metodoPago = await _context.MetodosPago
            .FirstOrDefaultAsync(m => m.MetodoPagoId == request.MetodoPagoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Método de pago {request.MetodoPagoId} no encontrado.");

        metodoPago.Nombre = request.Dto.Nombre;
        metodoPago.EstaActivo = request.Dto.EstaActivo;

        await _context.SaveChangesAsync(cancellationToken);

        return new MetodoPagoDto
        {
            MetodoPagoId = metodoPago.MetodoPagoId,
            Nombre = metodoPago.Nombre,
            EstaActivo = metodoPago.EstaActivo,
            CreadoEn = metodoPago.CreadoEn
        };
    }
}
