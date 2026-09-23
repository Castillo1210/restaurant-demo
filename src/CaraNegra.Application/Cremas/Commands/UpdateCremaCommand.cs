using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Cremas.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Cremas.Commands;

public record UpdateCremaCommand(int CremaId, UpdateCremaDto Dto) : IRequest<CremaDto>;

public class UpdateCremaCommandHandler : IRequestHandler<UpdateCremaCommand, CremaDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateCremaCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<CremaDto> Handle(UpdateCremaCommand request, CancellationToken cancellationToken)
    {
        var crema = await _context.Cremas
            .FirstOrDefaultAsync(c => c.CremaId == request.CremaId, cancellationToken)
            ?? throw new KeyNotFoundException($"Crema {request.CremaId} no encontrada.");

        crema.Nombre = request.Dto.Nombre;
        crema.Orden = request.Dto.Orden;
        crema.EstaActivo = request.Dto.EstaActivo;

        await _context.SaveChangesAsync(cancellationToken);

        return new CremaDto
        {
            CremaId = crema.CremaId,
            Nombre = crema.Nombre,
            Orden = crema.Orden,
            EstaActivo = crema.EstaActivo,
            CreadoEn = crema.CreadoEn
        };
    }
}
