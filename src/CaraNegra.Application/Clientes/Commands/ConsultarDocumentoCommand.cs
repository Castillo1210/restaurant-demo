using CaraNegra.Application.Clientes.DTOs;
using CaraNegra.Application.Common;
using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Clientes.Commands;

/// <summary>
/// Busca el nombre / razón social de un documento:
///   1. Primero en la base local de clientes (gratis e instantáneo).
///   2. Si no está, en la API externa (RENIEC para DNI, SUNAT para RUC) y el resultado se guarda
///      en la base de clientes para no volver a pagar esa consulta.
/// Es un "command" (no query) porque puede insertar el cliente nuevo.
/// </summary>
public record ConsultarDocumentoCommand(TipoDocumentoIdentidad Tipo, string Numero) : IRequest<ClienteConsultaDto>;

public class ConsultarDocumentoCommandHandler : IRequestHandler<ConsultarDocumentoCommand, ClienteConsultaDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IConsultaDocumentoService _consulta;

    public ConsultarDocumentoCommandHandler(IApplicationDbContext context, IConsultaDocumentoService consulta)
    {
        _context = context;
        _consulta = consulta;
    }

    public async Task<ClienteConsultaDto> Handle(ConsultarDocumentoCommand request, CancellationToken cancellationToken)
    {
        var numero = (request.Numero ?? string.Empty).Trim();

        var valido = request.Tipo switch
        {
            TipoDocumentoIdentidad.DNI => DocumentoIdentidadHelper.EsDniValido(numero),
            TipoDocumentoIdentidad.RUC => DocumentoIdentidadHelper.EsRucValido(numero),
            TipoDocumentoIdentidad.CarnetExtranjeria => DocumentoIdentidadHelper.EsCarnetExtranjeriaValido(numero),
            _ => false
        };
        if (!valido)
            throw new InvalidOperationException($"El número de {request.Tipo} no es válido");

        // 1) Base local de clientes
        var cliente = await _context.Clientes
            .FirstOrDefaultAsync(c => c.TipoDocumento == request.Tipo && c.NumeroDocumento == numero, cancellationToken);

        if (cliente is not null)
        {
            // RUC guardado sin dirección (clientes registrados antes de guardar la dirección, o
            // escritos a mano): se consulta UNA vez a SUNAT para completarla. Si la API falla, se
            // devuelve lo que hay (el cajero puede escribir la dirección).
            if (request.Tipo == TipoDocumentoIdentidad.RUC && cliente.Direccion is null)
            {
                try
                {
                    var sunat = await _consulta.ConsultarAsync(request.Tipo, numero, cancellationToken);
                    var direccion = ClienteRegistro.LimpiarDireccion(sunat?.Direccion);
                    if (direccion is not null)
                    {
                        cliente.Direccion = direccion;
                        await _context.SaveChangesAsync(cancellationToken);
                    }
                }
                catch (ConsultaDocumentoNoDisponibleException)
                {
                    // Sin API: se sigue con los datos locales.
                }
            }

            return new ClienteConsultaDto
            {
                TipoDocumento = cliente.TipoDocumento,
                NumeroDocumento = cliente.NumeroDocumento,
                Nombre = cliente.Nombre,
                Direccion = cliente.Direccion,
                Fuente = "Clientes",
                DesdeBaseLocal = true
            };
        }

        // Las APIs públicas solo cubren DNI (RENIEC) y RUC (SUNAT); el C.E. se registra a mano.
        if (request.Tipo == TipoDocumentoIdentidad.CarnetExtranjeria)
            throw new KeyNotFoundException("Carné de extranjería no registrado: ingrese el nombre del cliente");

        // 2) API externa (puede lanzar ConsultaDocumentoNoDisponibleException)
        var resultado = await _consulta.ConsultarAsync(request.Tipo, numero, cancellationToken)
            ?? throw new KeyNotFoundException(
                request.Tipo == TipoDocumentoIdentidad.RUC
                    ? $"El RUC {numero} no figura en SUNAT"
                    : $"El DNI {numero} no figura en RENIEC");

        // 3) Guardar en la base de clientes para las próximas compras
        await ClienteRegistro.GuardarAsync(_context, request.Tipo, numero, resultado.Nombre, cancellationToken,
            direccion: request.Tipo == TipoDocumentoIdentidad.RUC ? resultado.Direccion : null);
        await _context.SaveChangesAsync(cancellationToken);

        return new ClienteConsultaDto
        {
            TipoDocumento = request.Tipo,
            NumeroDocumento = numero,
            Nombre = ClienteRegistro.Normalizar(resultado.Nombre),
            Direccion = ClienteRegistro.LimpiarDireccion(resultado.Direccion),
            Estado = resultado.Estado,
            Condicion = resultado.Condicion,
            Fuente = _consulta.Proveedor,
            DesdeBaseLocal = false
        };
    }
}
