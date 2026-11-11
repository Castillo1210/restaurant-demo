using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Productos.Imagenes;

/// <summary>Reglas de la foto de un producto.</summary>
public static class ImagenProductoReglas
{
    /// <summary>
    /// Tope por foto. El navegador ya la reduce (máx. 800 px, ~30-80 KB) antes de subirla;
    /// este límite solo frena archivos enormes que lleguen por otro camino.
    /// </summary>
    public const int MaximoBytes = 1_500_000;

    /// <summary>
    /// Tipo real del archivo según sus primeros bytes (no se confía en el nombre ni en el
    /// Content-Type que manda el navegador). Solo JPEG, PNG y WebP; cualquier otra cosa → null.
    /// </summary>
    public static string? DetectarTipo(ReadOnlySpan<byte> datos)
    {
        if (datos.Length >= 3 && datos[0] == 0xFF && datos[1] == 0xD8 && datos[2] == 0xFF)
            return "image/jpeg";
        if (datos.Length >= 8 && datos[0] == 0x89 && datos[1] == 0x50 && datos[2] == 0x4E && datos[3] == 0x47
            && datos[4] == 0x0D && datos[5] == 0x0A && datos[6] == 0x1A && datos[7] == 0x0A)
            return "image/png";
        if (datos.Length >= 12 && datos[0] == (byte)'R' && datos[1] == (byte)'I' && datos[2] == (byte)'F' && datos[3] == (byte)'F'
            && datos[8] == (byte)'W' && datos[9] == (byte)'E' && datos[10] == (byte)'B' && datos[11] == (byte)'P')
            return "image/webp";
        return null;
    }
}

public record ImagenProductoResultado(int ProductoId, int ImagenVersion);

public record ArchivoImagenProducto(byte[] Datos, string TipoContenido, int Version);

// ───────────── Subir / cambiar ─────────────

public record GuardarImagenProductoCommand(int ProductoId, byte[] Datos) : IRequest<ImagenProductoResultado>;

public class GuardarImagenProductoCommandHandler : IRequestHandler<GuardarImagenProductoCommand, ImagenProductoResultado>
{
    private readonly IApplicationDbContext _context;

    public GuardarImagenProductoCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<ImagenProductoResultado> Handle(GuardarImagenProductoCommand request, CancellationToken cancellationToken)
    {
        var datos = request.Datos ?? Array.Empty<byte>();
        if (datos.Length == 0)
            throw new InvalidOperationException("No se recibió ninguna imagen");
        if (datos.Length > ImagenProductoReglas.MaximoBytes)
            throw new InvalidOperationException($"La imagen pesa {datos.Length / 1024} KB; el máximo es 1,5 MB");

        var tipo = ImagenProductoReglas.DetectarTipo(datos)
            ?? throw new InvalidOperationException("El archivo no es una imagen JPG, PNG o WebP");

        var producto = await _context.Productos
            .FirstOrDefaultAsync(p => p.ProductoId == request.ProductoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Producto {request.ProductoId} no encontrado");

        var imagen = await _context.ProductoImagenes
            .FirstOrDefaultAsync(i => i.ProductoId == request.ProductoId, cancellationToken);

        if (imagen is null)
        {
            imagen = new ProductoImagen { ProductoId = producto.ProductoId };
            _context.ProductoImagenes.Add(imagen);
        }

        imagen.Datos = datos;
        imagen.TipoContenido = tipo;
        imagen.ActualizadoEn = DateTime.UtcNow;
        producto.ImagenVersion += 1;

        await _context.SaveChangesAsync(cancellationToken);
        return new ImagenProductoResultado(producto.ProductoId, producto.ImagenVersion);
    }
}

// ───────────── Quitar ─────────────

public record QuitarImagenProductoCommand(int ProductoId) : IRequest<ImagenProductoResultado>;

public class QuitarImagenProductoCommandHandler : IRequestHandler<QuitarImagenProductoCommand, ImagenProductoResultado>
{
    private readonly IApplicationDbContext _context;

    public QuitarImagenProductoCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<ImagenProductoResultado> Handle(QuitarImagenProductoCommand request, CancellationToken cancellationToken)
    {
        var producto = await _context.Productos
            .FirstOrDefaultAsync(p => p.ProductoId == request.ProductoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Producto {request.ProductoId} no encontrado");

        var imagen = await _context.ProductoImagenes
            .FirstOrDefaultAsync(i => i.ProductoId == request.ProductoId, cancellationToken);
        if (imagen is not null)
            _context.ProductoImagenes.Remove(imagen);

        producto.ImagenVersion = 0;
        await _context.SaveChangesAsync(cancellationToken);
        return new ImagenProductoResultado(producto.ProductoId, 0);
    }
}

// ───────────── Leer (para mostrarla) ─────────────

public record GetImagenProductoQuery(int ProductoId) : IRequest<ArchivoImagenProducto?>;

public class GetImagenProductoQueryHandler : IRequestHandler<GetImagenProductoQuery, ArchivoImagenProducto?>
{
    private readonly IApplicationDbContext _context;

    public GetImagenProductoQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<ArchivoImagenProducto?> Handle(GetImagenProductoQuery request, CancellationToken cancellationToken)
    {
        return await _context.ProductoImagenes
            .AsNoTracking()
            .Where(i => i.ProductoId == request.ProductoId)
            .Select(i => new ArchivoImagenProducto(i.Datos, i.TipoContenido, i.Producto.ImagenVersion))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
