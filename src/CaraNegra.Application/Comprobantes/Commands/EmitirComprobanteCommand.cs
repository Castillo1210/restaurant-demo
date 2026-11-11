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
/// Emite el comprobante electrónico de un pedido ya pagado por completo.
///
/// Siempre reserva primero la serie-correlativo y guarda el comprobante (nunca se pierde un
/// número ni se bloquea la venta). Luego, según el proveedor configurado:
///   - "Simulado": queda "AceptadoSimulado" con un hash de ejemplo (demo, sin envío a SUNAT).
///   - "ApiSunat": se envía al PSE (sandbox o producción) y se guarda su respuesta: Aceptado,
///     Pendiente, Rechazado, ErrorEnvio o PorEnviar (sin conexión, se reintenta solo).
/// </summary>
public class EmitirComprobanteCommandHandler : IRequestHandler<EmitirComprobanteCommand, ComprobanteDto>
{
    // SUNAT exige identificar al cliente en boletas cuyo importe total supere S/ 700.
    private const decimal MontoMaximoBoletaSinDocumento = 700m;

    private readonly IApplicationDbContext _context;
    private readonly IDatosEmpresaProvider _empresaProvider;
    private readonly IFacturacionElectronicaService _facturacion;
    private DatosEmpresa _empresa = new();

    public EmitirComprobanteCommandHandler(IApplicationDbContext context, IDatosEmpresaProvider empresaProvider,
        IFacturacionElectronicaService facturacion)
    {
        _context = context;
        _empresaProvider = empresaProvider;
        _facturacion = facturacion;
    }

    public async Task<ComprobanteDto> Handle(EmitirComprobanteCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;

        // Doble toque en "Emitir" o dos cajas sobre el mismo pedido: el pedido se bloquea y la
        // segunda petición espera; al continuar encuentra el comprobante de la primera y se
        // rechaza. Así nunca salen dos boletas (dos envíos a SUNAT) para un mismo consumo.
        // El bloqueo dura solo hasta guardar el número; el envío al PSE va después, sin bloqueo.
        await using var tx = await _context.IniciarTransaccionAsync(cancellationToken);
        await _context.BloquearPedidoAsync(dto.PedidoId, cancellationToken);

        _empresa = await _empresaProvider.ObtenerAsync(cancellationToken);

        var pedido = await _context.Pedidos
            .Include(p => p.Pagos)
            .FirstOrDefaultAsync(p => p.PedidoId == dto.PedidoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Pedido {dto.PedidoId} no encontrado");

        if (pedido.EstadoPedido != EstadoPedido.Entregado)
            throw new InvalidOperationException("Solo se puede emitir el comprobante de un pedido pagado por completo");

        // Un comprobante rechazado por SUNAT o con error de envío no cuenta: se puede emitir otro.
        var existente = await _context.Comprobantes
            .Where(c => c.PedidoId == pedido.PedidoId
                && c.Estado != EstadoComprobante.Anulado
                && c.Estado != EstadoComprobante.Rechazado
                && c.Estado != EstadoComprobante.ErrorEnvio)
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
            EmisorRuc = _empresa.Ruc,
            EmisorRazonSocial = _empresa.RazonSocial,
            EmisorNombreComercial = _empresa.NombreComercial,
            EmisorDireccion = _empresa.Direccion,
            IgvPorcentaje = _empresa.IgvPorcentaje,
            Estado = _facturacion.EsSimulado ? EstadoComprobante.AceptadoSimulado : EstadoComprobante.PorEnviar,
            // Marca de "en envío": el reintento automático no lo toca durante el primer minuto, para no
            // mandarlo dos veces a la vez con el envío que hace la caja enseguida.
            UltimoEnvioEn = _facturacion.EsSimulado ? null : DateTime.UtcNow,
            Ambiente = _facturacion.Ambiente,
            EsSimulacion = _facturacion.Ambiente != "PRODUCCION"
        };

        // Registrar / actualizar el cliente en la base local (si se identificó con documento),
        // así la próxima compra se autocompleta sin consultar la API externa.
        await CaraNegra.Application.Clientes.ClienteRegistro.GuardarAsync(
            _context, tipoDoc, numeroDoc, nombre, cancellationToken,
            direccion: tipoDoc == TipoDocumentoIdentidad.RUC ? dto.ClienteDireccion : null);

        // Correlativo por serie. El índice único (Serie, Correlativo) protege contra dos cajas
        // emitiendo a la vez: si choca, se recalcula y se reintenta.
        const int maxIntentos = 3;
        for (var intento = 1; ; intento++)
        {
            var ultimo = await Numeracion.UltimoAsync(_context, _facturacion, serie, cancellationToken);

            comprobante.Correlativo = ultimo + 1;
            // El hash real (DigestValue de la firma) lo devuelve el proveedor; en la demo se usa uno de ejemplo.
            comprobante.HashResumen = _facturacion.EsSimulado ? CalcularHash(comprobante) : string.Empty;

            if (intento == 1) _context.Comprobantes.Add(comprobante);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                break;
            }
            catch (DbUpdateException) when (intento < maxIntentos)
            {
                // Otro comprobante tomó el mismo correlativo: reintentar con el siguiente.
                // (READ COMMITTED: el MAX de arriba ya ve el número que tomó la otra caja.)
            }
        }
        await tx.ConfirmarAsync(cancellationToken);

        var creado = await ComprobanteQueries.ConDetalle(_context)
            .FirstAsync(c => c.ComprobanteId == comprobante.ComprobanteId, cancellationToken);

        // Envío al PSE. El número ya quedó guardado: si esto falla o no hay internet, el comprobante
        // queda "PorEnviar" y el proceso automático lo reintenta.
        if (!_facturacion.EsSimulado)
        {
            await ComprobanteEnvio.ProcesarAsync(creado, _facturacion, cancellationToken,
                async ct => await _context.SaveChangesAsync(ct));
            await _context.SaveChangesAsync(cancellationToken);
        }

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
