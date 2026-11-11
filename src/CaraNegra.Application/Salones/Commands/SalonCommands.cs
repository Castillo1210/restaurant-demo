using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Salones.DTOs;
using CaraNegra.Application.Salones.Queries;
using CaraNegra.Domain.Entities;
using CaraNegra.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Salones.Commands;

// ---- Crear ----
public record CreateSalonCommand(CreateSalonDto Dto) : IRequest<SalonDto>;

public class CreateSalonCommandHandler : IRequestHandler<CreateSalonCommand, SalonDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IMediator _mediator;

    public CreateSalonCommandHandler(IApplicationDbContext context, IMediator mediator)
    {
        _context = context;
        _mediator = mediator;
    }

    public async Task<SalonDto> Handle(CreateSalonCommand request, CancellationToken cancellationToken)
    {
        var nombre = request.Dto.Nombre.Trim();

        if (await _context.Salones.AnyAsync(s => s.Nombre == nombre, cancellationToken))
            throw new InvalidOperationException($"Ya existe un salón llamado \"{nombre}\"");

        var salon = new Salon
        {
            Nombre = nombre,
            Descripcion = string.IsNullOrWhiteSpace(request.Dto.Descripcion) ? null : request.Dto.Descripcion.Trim(),
            Orden = request.Dto.Orden,
            EstaActivo = true
        };

        _context.Salones.Add(salon);
        await _context.SaveChangesAsync(cancellationToken);

        return await _mediator.Send(new GetSalonByIdQuery(salon.SalonId), cancellationToken);
    }
}

// ---- Editar (incluye activar / desactivar) ----
public record UpdateSalonCommand(int SalonId, UpdateSalonDto Dto) : IRequest<SalonDto>;

public class UpdateSalonCommandHandler : IRequestHandler<UpdateSalonCommand, SalonDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IMediator _mediator;

    public UpdateSalonCommandHandler(IApplicationDbContext context, IMediator mediator)
    {
        _context = context;
        _mediator = mediator;
    }

    public async Task<SalonDto> Handle(UpdateSalonCommand request, CancellationToken cancellationToken)
    {
        var salon = await _context.Salones
            .FirstOrDefaultAsync(s => s.SalonId == request.SalonId, cancellationToken)
            ?? throw new KeyNotFoundException($"Salón {request.SalonId} no encontrado");

        var nombre = request.Dto.Nombre.Trim();
        if (nombre != salon.Nombre
            && await _context.Salones.AnyAsync(s => s.Nombre == nombre && s.SalonId != salon.SalonId, cancellationToken))
        {
            throw new InvalidOperationException($"Ya existe otro salón llamado \"{nombre}\"");
        }

        if (salon.EstaActivo && !request.Dto.EstaActivo)
            await ValidarSinMesasOcupadas(salon.SalonId, cancellationToken);

        salon.Nombre = nombre;
        salon.Descripcion = string.IsNullOrWhiteSpace(request.Dto.Descripcion) ? null : request.Dto.Descripcion.Trim();
        salon.Orden = request.Dto.Orden;
        salon.EstaActivo = request.Dto.EstaActivo;

        await _context.SaveChangesAsync(cancellationToken);

        return await _mediator.Send(new GetSalonByIdQuery(salon.SalonId), cancellationToken);
    }

    private async Task ValidarSinMesasOcupadas(int salonId, CancellationToken ct)
    {
        var ocupadas = await _context.Mesas.AnyAsync(m => m.SalonId == salonId && m.Estado == EstadoMesa.Ocupada, ct);
        if (ocupadas)
            throw new InvalidOperationException("No se puede desactivar un salón con mesas ocupadas. Cobre o cierre esos pedidos primero.");
    }
}

// ---- Desactivar (soft delete) ----
/// <summary>
/// Desactiva el salón (no se borra: sus mesas y el historial de pedidos siguen referenciándolo).
/// Un salón inactivo desaparece del mapa del mozo. Se reactiva editándolo con EstaActivo = true.
/// </summary>
public record DeleteSalonCommand(int SalonId) : IRequest<bool>;

public class DeleteSalonCommandHandler : IRequestHandler<DeleteSalonCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public DeleteSalonCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<bool> Handle(DeleteSalonCommand request, CancellationToken cancellationToken)
    {
        var salon = await _context.Salones
            .FirstOrDefaultAsync(s => s.SalonId == request.SalonId, cancellationToken)
            ?? throw new KeyNotFoundException($"Salón {request.SalonId} no encontrado");

        var ocupadas = await _context.Mesas
            .AnyAsync(m => m.SalonId == salon.SalonId && m.Estado == EstadoMesa.Ocupada, cancellationToken);
        if (ocupadas)
            throw new InvalidOperationException("No se puede desactivar un salón con mesas ocupadas. Cobre o cierre esos pedidos primero.");

        salon.EstaActivo = false;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
