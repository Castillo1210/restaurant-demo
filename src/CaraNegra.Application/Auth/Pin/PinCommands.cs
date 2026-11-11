using CaraNegra.Application.Auth.Interfaces;
using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Auth.Pin;

internal static class DispositivoLookup
{
    /// <summary>Busca el dispositivo por el hash de su token; lanza si no existe (no registrado o revocado).</summary>
    public static async Task<DispositivoAutorizado> ObtenerAsync(
        IApplicationDbContext context, string? token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 200)
            throw new DispositivoNoAutorizadoException();

        var hash = TokenDispositivo.Hash(token);
        return await context.DispositivosAutorizados.FirstOrDefaultAsync(d => d.TokenHash == hash, ct)
            ?? throw new DispositivoNoAutorizadoException();
    }
}

// ───────────────────────── Pantalla de ingreso con PIN ─────────────────────────

/// <summary>Nombres del personal con PIN (MOZO / COCINA activos) para un dispositivo registrado.</summary>
public record GetPantallaPinQuery(string? TokenDispositivo) : IRequest<PantallaPinDto>;

public class GetPantallaPinQueryHandler : IRequestHandler<GetPantallaPinQuery, PantallaPinDto>
{
    private readonly IApplicationDbContext _context;

    public GetPantallaPinQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<PantallaPinDto> Handle(GetPantallaPinQuery request, CancellationToken cancellationToken)
    {
        var dispositivo = await DispositivoLookup.ObtenerAsync(_context, request.TokenDispositivo, cancellationToken);

        // Comparaciones explícitas (no array.Contains): en .NET 10 Contains sobre un arreglo dentro
        // de una consulta EF falla al traducirse.
        var usuarios = await _context.Usuarios
            .AsNoTracking()
            .Where(u => u.EsVerificado && u.PinHash != null
                && (u.Rol.Nombre == "MOZO" || u.Rol.Nombre == "COCINA"))
            .OrderBy(u => u.NombreCompleto)
            .Select(u => new UsuarioPinDto
            {
                UsuarioId = u.UsuarioId,
                NombreCompleto = u.NombreCompleto,
                Rol = u.Rol.Nombre
            })
            .ToListAsync(cancellationToken);

        return new PantallaPinDto
        {
            DispositivoId = dispositivo.DispositivoAutorizadoId,
            Dispositivo = dispositivo.Nombre,
            CierreInactividadMinutos = dispositivo.CierreInactividadMinutos,
            Usuarios = usuarios
        };
    }
}

// ───────────────────────── Login con PIN ─────────────────────────

public record LoginConPinCommand(string? TokenDispositivo, LoginPinRequest Request) : IRequest<LoginPinResponse>;

public class LoginConPinCommandHandler : IRequestHandler<LoginConPinCommand, LoginPinResponse>
{
    private const string MensajeGenerico = "PIN incorrecto.";

    private readonly IApplicationDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly IPasswordService _passwordService;

    public LoginConPinCommandHandler(IApplicationDbContext context, IJwtService jwtService, IPasswordService passwordService)
    {
        _context = context;
        _jwtService = jwtService;
        _passwordService = passwordService;
    }

    public async Task<LoginPinResponse> Handle(LoginConPinCommand request, CancellationToken cancellationToken)
    {
        var dispositivo = await DispositivoLookup.ObtenerAsync(_context, request.TokenDispositivo, cancellationToken);

        var usuario = await _context.Usuarios
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.UsuarioId == request.Request.UsuarioId, cancellationToken);

        if (usuario is null || usuario.PinHash is null || !usuario.EsVerificado || !PinReglas.RolPermitePin(usuario.Rol?.Nombre))
            throw new UnauthorizedAccessException(MensajeGenerico);

        var ahora = DateTime.UtcNow;

        if (usuario.PinBloqueadoHasta is { } hasta && hasta > ahora)
            throw new UnauthorizedAccessException(MensajeBloqueo(hasta, ahora));

        if (!_passwordService.VerificarPassword(request.Request.Pin, usuario.PinHash))
        {
            usuario.PinIntentosFallidos++;
            string mensaje;

            if (usuario.PinIntentosFallidos >= PinReglas.MaxIntentosFallidos)
            {
                usuario.PinIntentosFallidos = 0;
                usuario.PinBloqueadoHasta = ahora.AddMinutes(PinReglas.MinutosBloqueo);
                mensaje = MensajeBloqueo(usuario.PinBloqueadoHasta.Value, ahora);
            }
            else
            {
                var quedan = PinReglas.MaxIntentosFallidos - usuario.PinIntentosFallidos;
                mensaje = quedan == 1
                    ? $"{MensajeGenerico} Te queda 1 intento antes de que se bloquee."
                    : $"{MensajeGenerico} Te quedan {quedan} intentos.";
            }

            await _context.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAccessException(mensaje);
        }

        usuario.PinIntentosFallidos = 0;
        usuario.PinBloqueadoHasta = null;
        usuario.UltimoAccesoEn = ahora;
        dispositivo.UltimoUsoEn = ahora;
        await _context.SaveChangesAsync(cancellationToken);

        return new LoginPinResponse
        {
            Token = _jwtService.GenerarToken(usuario),
            UsuarioId = usuario.UsuarioId,
            NombreUsuario = usuario.NombreUsuario,
            NombreCompleto = usuario.NombreCompleto,
            Rol = usuario.Rol!.Nombre,
            Expiracion = _jwtService.ObtenerExpiracion(),
            CierreInactividadMinutos = dispositivo.CierreInactividadMinutos
        };
    }

    private static string MensajeBloqueo(DateTime hasta, DateTime ahora)
    {
        var minutos = Math.Max(1, (int)Math.Ceiling((hasta - ahora).TotalMinutes));
        return $"PIN bloqueado por demasiados intentos. Espera {minutos} min, entra con tu contraseña " +
               "o pide al administrador que lo desbloquee.";
    }
}

// ───────────────────────── Administración del PIN (ADMIN) ─────────────────────────

public record AsignarPinCommand(int UsuarioId, AsignarPinDto Dto) : IRequest<Unit>;

public class AsignarPinCommandHandler : IRequestHandler<AsignarPinCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordService _passwordService;

    public AsignarPinCommandHandler(IApplicationDbContext context, IPasswordService passwordService)
    {
        _context = context;
        _passwordService = passwordService;
    }

    public async Task<Unit> Handle(AsignarPinCommand request, CancellationToken cancellationToken)
    {
        var usuario = await _context.Usuarios
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.UsuarioId == request.UsuarioId, cancellationToken)
            ?? throw new KeyNotFoundException($"Usuario {request.UsuarioId} no encontrado");

        if (!PinReglas.RolPermitePin(usuario.Rol?.Nombre))
            throw new InvalidOperationException(
                "El PIN solo se usa para MOZO y COCINA. CAJERO y ADMIN ingresan siempre con contraseña.");

        usuario.PinHash = _passwordService.HashPassword(request.Dto.Pin);
        usuario.PinIntentosFallidos = 0;
        usuario.PinBloqueadoHasta = null;
        usuario.ActualizadoEn = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public record QuitarPinCommand(int UsuarioId) : IRequest<Unit>;

public class QuitarPinCommandHandler : IRequestHandler<QuitarPinCommand, Unit>
{
    private readonly IApplicationDbContext _context;

    public QuitarPinCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<Unit> Handle(QuitarPinCommand request, CancellationToken cancellationToken)
    {
        var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.UsuarioId == request.UsuarioId, cancellationToken)
            ?? throw new KeyNotFoundException($"Usuario {request.UsuarioId} no encontrado");

        usuario.PinHash = null;
        usuario.PinIntentosFallidos = 0;
        usuario.PinBloqueadoHasta = null;
        usuario.ActualizadoEn = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public record DesbloquearPinCommand(int UsuarioId) : IRequest<Unit>;

public class DesbloquearPinCommandHandler : IRequestHandler<DesbloquearPinCommand, Unit>
{
    private readonly IApplicationDbContext _context;

    public DesbloquearPinCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<Unit> Handle(DesbloquearPinCommand request, CancellationToken cancellationToken)
    {
        var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.UsuarioId == request.UsuarioId, cancellationToken)
            ?? throw new KeyNotFoundException($"Usuario {request.UsuarioId} no encontrado");

        usuario.PinIntentosFallidos = 0;
        usuario.PinBloqueadoHasta = null;
        await _context.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

// ───────────────────────── Dispositivos registrados (ADMIN) ─────────────────────────

internal static class DispositivoMapper
{
    public static DispositivoDto ToDto(DispositivoAutorizado d) => new()
    {
        DispositivoId = d.DispositivoAutorizadoId,
        Nombre = d.Nombre,
        CierreInactividadMinutos = d.CierreInactividadMinutos,
        CreadoEn = d.CreadoEn,
        UltimoUsoEn = d.UltimoUsoEn,
        RegistradoPor = d.RegistradoPor
    };
}

public record GetDispositivosQuery : IRequest<List<DispositivoDto>>;

public class GetDispositivosQueryHandler : IRequestHandler<GetDispositivosQuery, List<DispositivoDto>>
{
    private readonly IApplicationDbContext _context;

    public GetDispositivosQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<DispositivoDto>> Handle(GetDispositivosQuery request, CancellationToken cancellationToken)
    {
        var dispositivos = await _context.DispositivosAutorizados
            .AsNoTracking()
            .OrderBy(d => d.Nombre)
            .ToListAsync(cancellationToken);
        return dispositivos.Select(DispositivoMapper.ToDto).ToList();
    }
}

/// <summary>Registra el dispositivo desde el que se llama (el ADMIN lo hace en ese celular o tablet).</summary>
public record RegistrarDispositivoCommand(RegistrarDispositivoDto Dto, string? RegistradoPor) : IRequest<DispositivoRegistradoDto>;

public class RegistrarDispositivoCommandHandler : IRequestHandler<RegistrarDispositivoCommand, DispositivoRegistradoDto>
{
    private readonly IApplicationDbContext _context;

    public RegistrarDispositivoCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<DispositivoRegistradoDto> Handle(RegistrarDispositivoCommand request, CancellationToken cancellationToken)
    {
        var token = TokenDispositivo.Generar();
        var dispositivo = new DispositivoAutorizado
        {
            Nombre = request.Dto.Nombre.Trim(),
            TokenHash = TokenDispositivo.Hash(token),
            CierreInactividadMinutos = request.Dto.CierreInactividadMinutos,
            RegistradoPor = request.RegistradoPor
        };

        _context.DispositivosAutorizados.Add(dispositivo);
        await _context.SaveChangesAsync(cancellationToken);

        return new DispositivoRegistradoDto { Dispositivo = DispositivoMapper.ToDto(dispositivo), Token = token };
    }
}

/// <summary>Revoca (elimina) un dispositivo: desde ese momento ya no puede ingresar con PIN.</summary>
public record RevocarDispositivoCommand(int DispositivoId) : IRequest<Unit>;

public class RevocarDispositivoCommandHandler : IRequestHandler<RevocarDispositivoCommand, Unit>
{
    private readonly IApplicationDbContext _context;

    public RevocarDispositivoCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<Unit> Handle(RevocarDispositivoCommand request, CancellationToken cancellationToken)
    {
        var dispositivo = await _context.DispositivosAutorizados
            .FirstOrDefaultAsync(d => d.DispositivoAutorizadoId == request.DispositivoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Dispositivo {request.DispositivoId} no encontrado");

        _context.DispositivosAutorizados.Remove(dispositivo);
        await _context.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
