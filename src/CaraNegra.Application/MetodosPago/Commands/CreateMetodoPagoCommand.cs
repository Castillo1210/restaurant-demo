using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.MetodosPago.DTOs;
using CaraNegra.Domain.Entities;
using MediatR;

namespace CaraNegra.Application.MetodosPago.Commands;

public record CreateMetodoPagoCommand(CreateMetodoPagoDto Dto) : IRequest<MetodoPagoDto>;

public class CreateMetodoPagoCommandHandler : IRequestHandler<CreateMetodoPagoCommand, MetodoPagoDto>
{
    private readonly IApplicationDbContext _context;

    public CreateMetodoPagoCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<MetodoPagoDto> Handle(CreateMetodoPagoCommand request, CancellationToken cancellationToken)
    {
        var metodoPago = new MetodoPago
        {
            Nombre = request.Dto.Nombre,
            EstaActivo = true
        };

        _context.MetodosPago.Add(metodoPago);
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
