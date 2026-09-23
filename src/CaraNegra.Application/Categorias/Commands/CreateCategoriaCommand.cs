using CaraNegra.Application.Categorias.DTOs;
using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Domain.Entities;
using MediatR;

namespace CaraNegra.Application.Categorias.Commands;

public record CreateCategoriaCommand(CreateCategoriaDto Dto) : IRequest<CategoriaDto>;

public class CreateCategoriaCommandHandler : IRequestHandler<CreateCategoriaCommand, CategoriaDto>
{
    private readonly IApplicationDbContext _context;

    public CreateCategoriaCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<CategoriaDto> Handle(CreateCategoriaCommand request, CancellationToken cancellationToken)
    {
        var categoria = new Categoria
        {
            Nombre = request.Dto.Nombre,
            Descripcion = request.Dto.Descripcion,
            EstaActivo = true
        };

        _context.Categorias.Add(categoria);
        await _context.SaveChangesAsync(cancellationToken);

        return new CategoriaDto
        {
            CategoriaId = categoria.CategoriaId,
            Nombre = categoria.Nombre,
            Descripcion = categoria.Descripcion,
            EstaActivo = categoria.EstaActivo,
            CreadoEn = categoria.CreadoEn
        };
    }
}