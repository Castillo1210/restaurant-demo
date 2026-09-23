using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Cremas.DTOs;
using CaraNegra.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Cremas.Commands;

public record CreateCremaCommand(CreateCremaDto Dto) : IRequest<CremaDto>;

public class CreateCremaCommandHandler : IRequestHandler<CreateCremaCommand, CremaDto>
{
    private readonly IApplicationDbContext _context;

    public CreateCremaCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<CremaDto> Handle(CreateCremaCommand request, CancellationToken cancellationToken)
    {
        // Se agrega al final de la lista por defecto; el orden se puede ajustar luego
        // desde el panel de administración (subir/bajar).
        var maxOrden = await _context.Cremas.Select(c => (int?)c.Orden).MaxAsync(cancellationToken) ?? 0;

        var crema = new Crema
        {
            Nombre = request.Dto.Nombre,
            Orden = maxOrden + 1,
            EstaActivo = true
        };

        _context.Cremas.Add(crema);
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
