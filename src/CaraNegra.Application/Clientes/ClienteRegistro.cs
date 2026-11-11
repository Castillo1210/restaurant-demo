using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Domain.Entities;
using CaraNegra.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Clientes;

/// <summary>Alta / actualización de clientes en la base local (sin guardar cambios).</summary>
public static class ClienteRegistro
{
    public static string Normalizar(string? texto) =>
        string.Join(' ', (texto ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    /// <summary>
    /// Crea el cliente si no existe; si existe y el nombre o la dirección cambiaron (p.ej. el
    /// cajero los corrigió a mano), los actualiza. Una dirección vacía no borra la guardada.
    /// No llama a SaveChanges: lo hace quien invoca.
    /// </summary>
    public static async Task<Cliente?> GuardarAsync(IApplicationDbContext context, TipoDocumentoIdentidad tipo,
        string? numero, string? nombre, CancellationToken ct, string? direccion = null)
    {
        if (tipo == TipoDocumentoIdentidad.SinDocumento || string.IsNullOrWhiteSpace(numero) || string.IsNullOrWhiteSpace(nombre))
            return null;

        numero = numero.Trim();
        var nombreNormalizado = Normalizar(nombre);
        var direccionLimpia = LimpiarDireccion(direccion);

        var cliente = await context.Clientes
            .FirstOrDefaultAsync(c => c.TipoDocumento == tipo && c.NumeroDocumento == numero, ct);

        if (cliente is null)
        {
            cliente = new Cliente
            {
                TipoDocumento = tipo,
                NumeroDocumento = numero,
                Nombre = nombreNormalizado,
                Direccion = direccionLimpia
            };
            context.Clientes.Add(cliente);
        }
        else
        {
            if (cliente.Nombre != nombreNormalizado) cliente.Nombre = nombreNormalizado;
            if (direccionLimpia is not null && cliente.Direccion != direccionLimpia) cliente.Direccion = direccionLimpia;
        }

        return cliente;
    }

    /// <summary>Espacios normalizados, máx. 300 caracteres; vacío o "-" = sin dirección.</summary>
    public static string? LimpiarDireccion(string? direccion)
    {
        var d = string.Join(' ', (direccion ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (d.Length == 0 || d == "-") return null;
        return d.Length > 300 ? d[..300] : d;
    }
}
