using System.Security.Cryptography;
using System.Text;
using CaraNegra.Application.Common;
using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Comprobantes.DTOs;
using CaraNegra.Domain.Entities;
using CaraNegra.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Comprobantes.Commands;

public record EmitirComprobanteCommand(EmitirComprobanteDto Dto, int UsuarioId) : IRequest<ComprobanteDto>;

/// <summary>
/// Emite (SIMULA) un comprobante electrónico para un pedido ya pagado por completo.
///
/// Genera todo lo que tendría un comprobante real — serie y correlativo propios por tipo,
/// base imponible e IGV, hash y cadena QR — pero NO firma el XML ni lo envía a SUNAT/OSE.
/// El estado queda como "AceptadoSimulado" para imitar la respuesta de SUNAT en la demo.
/// Para pasar a producción, este handler es el punto donde se integraría el proveedor OSE/PSE.
/// </summary>
public class EmitirComprobanteCommandHandler : IRequestHandler<EmitirComprobanteCommand, ComprobanteDto>
{
    // SUNAT exige identificar al cliente en boletas cuyo importe total supere S/ 700.
    private const decimal MontoMaximoBoletaSinDocumento = 700m;

    private readonly IApplicationDbContext _context;
    private readonly DatosEmpresa _empresa;

    public EmitirComprobanteCommandHandler(IApplicationDbContext context, DatosEmpresa empresa)
    {
        _context = context;
        _empresa = empresa;
    }

    public async Task<ComprobanteDto> Handle(EmitirComprobanteCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;

        var pedido = await _context.Pedidos
            .Include(p => p.Pagos)
            .FirstOrDefaultAsync(p => p.PedidoId == dto.PedidoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Pedido {dto.PedidoId} no encontrado");

        if (pedido.EstadoPedido != EstadoPedido.Entregado)
            throw new InvalidOperationException("Solo se puede emitir el comprobante de un pedido pagado por completo");

        var existente = await _context.Comprobantes
            .Where(c => c.PedidoId == pedido.PedidoId && c.Estado != EstadoComprobante.Anulado)
            .Select(c => new { c.Serie, c.Correlativo })
            .FirstOrDefaultAsync(cancellationToken);

        if (existente != null)
            throw new InvalidOperationException(
                $"El pedido ya tiene el comprobante {ComprobanteMapper.FormatearNumero(existente.Serie, existente.Correlativo)}");

        var tipoDoc = dto.ClienteTipoDocumento;
        var numeroDoc = string.IsNullOrWhiteSpace(dto.ClienteNumeroDocumento) ? null : dto.ClienteNumeroDocumento.Trim();
        var nombre = dto.ClienteNombre?.Trim();

        if (dto.TipoComprobante == TipoComprobante.Boleta
            && pedido.Total > MontoMaximoBoletaSinDocumento
            && (tipoDoc == TipoDocumentoIdentidad.SinDocumento || numeroDoc == null))
        {
            throw new InvalidOperationException(
                $"Para boletas mayores a S/ {MontoMaximoBoletaSinDocumento:0.00} SUNAT exige el DNI o CE del cliente");
        }

        if (tipoDoc == TipoDocumentoIdentidad.SinDocumento)
        {
            numeroDoc = null;
            if (string.IsNullOrWhiteSpace(nombre)) nombre = "CLIENTES VARIOS";
        }

        // Importes: los precios de carta incluyen IGV → se desglosa la base imponible.
        var factorIgv = 1 + _empresa.IgvPorcentaje / 100m;
        var total = pedido.Total;
        var opGravada = Math.Round(total / factorIgv, 2, MidpointRounding.AwayFromZero);
        var igv = total - opGravada;
        var descuento = Math.Max(0, pedido.SubTotal - pedido.Total);

        var serie = dto.TipoComprobante == TipoComprobante.Factura ? _empresa.SerieFactura : _empresa.SerieBoleta;

        var comprobante = new Comprobante
        {
            PedidoId = pedido.PedidoId,
            UsuarioId = request.UsuarioId,
            TipoComprobante = dto.TipoComprobante,
            Serie = serie,
            ClienteTipoDocumento = tipoDoc,
            ClienteNumeroDocumento = numeroDoc,
            ClienteNombre = (nombre ?? string.Empty).ToUpperInvariant(),
            ClienteDireccion = string.IsNullOrWhiteSpace(dto.ClienteDireccion) ? null : dto.ClienteDireccion.Trim(),
            OpGravada = opGravada,
            Igv = igv,
            Descuento = descuento,
            Total = total,
            FechaEmision = DateTime.UtcNow,
            Estado = EstadoComprobante.AceptadoSimulado,
            EsSimulacion = true
        };

        // Correlativo por serie. El índice único (Serie, Correlativo) protege contra dos cajas
        // emitiendo a la vez: si choca, se recalcula y se reintenta.
        const int maxIntentos = 3;
        for (var intento = 1; ; intento++)
        {
            var ultimo = await _context.Comprobantes
                .Where(c => c.Serie == serie)
                .MaxAsync(c => (int?)c.Correlativo, cancellationToken) ?? 0;

            comprobante.Correlativo = ultimo + 1;
            comprobante.HashResumen = CalcularHash(comprobante);

            if (intento == 1) _context.Comprobantes.Add(comprobante);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                break;
            }
            catch (DbUpdateException) when (intento < maxIntentos)
            {
                // Otro comprobante tomó el mismo correlativo: reintentar con el siguiente.
            }
        }

        var creado = await ComprobanteQueries.ConDetalle(_context)
            .FirstAsync(c => c.ComprobanteId == comprobante.ComprobanteId, cancellationToken);

        return ComprobanteMapper.ToDto(creado, _empresa);
    }

    /// <summary>
    /// "Resumen" (DigestValue) SIMULADO: en un comprobante real es el hash de la firma digital
    /// del XML. Aquí se usa un SHA-256 de los datos clave solo para que el QR y el ticket
    /// tengan un valor con la forma correcta.
    /// </summary>
    private string CalcularHash(Comprobante c)
    {
        var datos = $"{_empresa.Ruc}|{c.TipoComprobante}|{c.Serie}|{c.Correlativo}|{c.Total:0.00}|{c.FechaEmision:O}|{c.ClienteNumeroDocumento}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(datos));
        return Convert.ToBase64String(hash)[..28];
    }
}
