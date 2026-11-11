using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Application.Comprobantes;
using CaraNegra.Domain.Entities;
using CaraNegra.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CaraNegra.Application.Jornadas;

/// <summary>Cálculos del resumen de una jornada y del arqueo esperado por medio de pago.</summary>
public static class JornadaCalculos
{
    public static JornadaDto ToDto(Jornada j) => new()
    {
        JornadaId = j.JornadaId,
        FechaOperativa = j.FechaOperativa.Date,
        Estado = j.Estado,
        AbiertaEn = j.AbiertaEn,
        AbiertaPor = j.AbiertaPor,
        FondoInicial = j.FondoInicial,
        ObservacionApertura = j.ObservacionApertura,
        CerradaEn = j.CerradaEn,
        CerradaPor = j.CerradaPor,
        ObservacionCierre = j.ObservacionCierre,
        EsDeDiaAnterior = j.Estado == EstadoJornada.Abierta && j.FechaOperativa.Date < JornadaReglas.HoyLima(),
        TotalVentas = j.TotalVentas,
        CantidadPedidos = j.CantidadPedidos,
        DiferenciaTotal = j.DiferenciaTotal
    };

    public static MovimientoCajaDto ToDto(MovimientoCaja m) => new()
    {
        MovimientoCajaId = m.MovimientoCajaId,
        Tipo = m.Tipo,
        Monto = m.Monto,
        Motivo = m.Motivo,
        RegistradoPor = m.RegistradoPor,
        CreadoEn = m.CreadoEn,
        EstaAnulado = m.EstaAnulado,
        AnuladoPor = m.AnuladoPor
    };

    /// <summary>
    /// Lo que debería haber por medio de pago. Incluye todos los medios activos (aunque no tengan
    /// ventas, para que el cajero cuente todos) y los que tuvieron cobros en la jornada.
    /// Efectivo esperado = fondo inicial + cobros en efectivo + ingresos − egresos.
    /// </summary>
    public static async Task<List<ArqueoDto>> CalcularEsperadoAsync(IApplicationDbContext context, Jornada jornada, CancellationToken ct)
    {
        var jornadaId = jornada.JornadaId;

        var cobros = await context.Pagos
            .AsNoTracking()
            .Where(p => p.JornadaId == jornadaId && !p.EstaAnulado)
            .GroupBy(p => p.MetodoPagoId)
            .Select(g => new { MetodoPagoId = g.Key, Total = g.Sum(p => p.Monto) })
            .ToListAsync(ct);

        var idsConCobros = cobros.Select(c => c.MetodoPagoId).ToList();
        var metodos = await context.MetodosPago
            .AsNoTracking()
            .OrderBy(m => m.MetodoPagoId)
            .ToListAsync(ct);
        metodos = metodos.Where(m => m.EstaActivo || idsConCobros.Contains(m.MetodoPagoId)).ToList();

        var movimientos = await context.MovimientosCaja
            .AsNoTracking()
            .Where(m => m.JornadaId == jornadaId && !m.EstaAnulado)
            .ToListAsync(ct);
        var ingresos = movimientos.Where(m => m.Tipo == TipoMovimientoCaja.Ingreso).Sum(m => m.Monto);
        var egresos = movimientos.Where(m => m.Tipo == TipoMovimientoCaja.Egreso).Sum(m => m.Monto);

        var arqueo = metodos.Select(m =>
        {
            var ventas = cobros.FirstOrDefault(c => c.MetodoPagoId == m.MetodoPagoId)?.Total ?? 0m;
            var esEfectivo = JornadaReglas.EsEfectivo(m.Nombre);
            return new ArqueoDto
            {
                MetodoPagoId = m.MetodoPagoId,
                MetodoPago = m.Nombre,
                EsEfectivo = esEfectivo,
                Ventas = ventas,
                Esperado = esEfectivo ? jornada.FondoInicial + ventas + ingresos - egresos : ventas
            };
        }).ToList();

        // Si no existe un medio "Efectivo" igual hay que arquear el fondo y los movimientos.
        if (!arqueo.Any(a => a.EsEfectivo))
        {
            arqueo.Insert(0, new ArqueoDto
            {
                MetodoPago = "Efectivo",
                EsEfectivo = true,
                Esperado = jornada.FondoInicial + ingresos - egresos
            });
        }

        // Efectivo primero.
        return arqueo.OrderByDescending(a => a.EsEfectivo).ToList();
    }

    public static async Task<ResumenJornadaDto> ConstruirResumenAsync(IApplicationDbContext context, Jornada jornada,
        bool incluirMontos, CancellationToken ct)
    {
        var jornadaId = jornada.JornadaId;
        var cerrada = jornada.Estado == EstadoJornada.Cerrada;
        incluirMontos = incluirMontos || cerrada;

        var pedidos = await context.Pedidos
            .AsNoTracking()
            .Include(p => p.Usuario)
            .Include(p => p.Mesa).ThenInclude(m => m.Salon)
            .Include(p => p.DetallesPedidos).ThenInclude(d => d.Producto)
            .Where(p => p.JornadaId == jornadaId)
            .ToListAsync(ct);

        var cobrados = pedidos.Where(p => p.EstadoPedido == EstadoPedido.Entregado).ToList();

        var pagos = await context.Pagos
            .AsNoTracking()
            .Include(p => p.MetodoPago)
            .Where(p => p.JornadaId == jornadaId)
            .ToListAsync(ct);
        var pagosValidos = pagos.Where(p => !p.EstaAnulado).ToList();
        var anulados = pagos.Where(p => p.EstaAnulado).ToList();

        var movimientos = await context.MovimientosCaja
            .AsNoTracking()
            .Where(m => m.JornadaId == jornadaId)
            .OrderBy(m => m.CreadoEn)
            .ToListAsync(ct);
        var movValidos = movimientos.Where(m => !m.EstaAnulado).ToList();

        var comprobantes = await context.Comprobantes
            .AsNoTracking()
            .Where(c => c.Pedido.JornadaId == jornadaId)
            .Select(c => new { c.TipoComprobante, c.Serie, c.Correlativo, c.Total, c.Estado })
            .ToListAsync(ct);

        var totalVentas = pagosValidos.Sum(p => p.Monto);

        var cambiosPrecio = await context.CambiosPrecioPedido
            .AsNoTracking()
            .Where(c => c.JornadaId == jornadaId)
            .OrderBy(c => c.CambiadoEn)
            .ToListAsync(ct);

        var anulacionesProductos = await context.ProductosAnulados
            .AsNoTracking()
            .Where(a => a.JornadaId == jornadaId)
            .OrderBy(a => a.AnuladoEn)
            .ToListAsync(ct);

        var resumen = new ResumenJornadaDto
        {
            Jornada = ToDto(jornada),
            IncluyeMontos = incluirMontos,
            CantidadPedidos = cobrados.Count,
            PedidosCancelados = pedidos.Count(p => p.EstadoPedido == EstadoPedido.Cancelado),
            PagosAnulados = anulados.Count,
            ProductosAnulados = anulacionesProductos.Sum(a => a.Cantidad),
            CambiosPrecio = cambiosPrecio.Select(CaraNegra.Application.Pedidos.CambioPrecioDto.Desde).ToList(),
            // Quién quitó qué y por qué: siempre visible (los importes solo con montos, abajo).
            Anulaciones = anulacionesProductos.Select(a =>
            {
                var dto = CaraNegra.Application.Pedidos.ProductoAnuladoDto.Desde(a);
                if (!incluirMontos) { dto.Importe = 0; dto.PrecioUnitario = 0; }
                return dto;
            }).ToList(),
            Movimientos = movimientos.Select(ToDto).ToList(),
            TotalIngresos = movValidos.Where(m => m.Tipo == TipoMovimientoCaja.Ingreso).Sum(m => m.Monto),
            TotalEgresos = movValidos.Where(m => m.Tipo == TipoMovimientoCaja.Egreso).Sum(m => m.Monto),
        };

        // Comprobantes (números, no montos): siempre visibles.
        var validos = comprobantes.Where(c => !ComprobanteEnvio.NoEsValido(c.Estado)).ToList();
        resumen.Comprobantes = new ResumenComprobantesDto
        {
            Boletas = validos.Count(c => c.TipoComprobante == TipoComprobante.Boleta),
            RangoBoletas = RangoNumeros(validos.Where(c => c.TipoComprobante == TipoComprobante.Boleta).Select(c => (c.Serie, c.Correlativo))),
            Facturas = validos.Count(c => c.TipoComprobante == TipoComprobante.Factura),
            RangoFacturas = RangoNumeros(validos.Where(c => c.TipoComprobante == TipoComprobante.Factura).Select(c => (c.Serie, c.Correlativo))),
            TotalFacturado = incluirMontos ? validos.Sum(c => c.Total) : 0,
            PorConfirmar = comprobantes.Count(c => ComprobanteEnvio.EsPendienteDeSunat(c.Estado)),
            ConError = comprobantes.Count(c => c.Estado is EstadoComprobante.Rechazado or EstadoComprobante.ErrorEnvio)
        };

        if (!incluirMontos)
            return resumen;

        resumen.TotalVentas = totalVentas;
        resumen.TicketPromedio = cobrados.Count > 0 ? Math.Round(cobrados.Sum(p => p.Total) / cobrados.Count, 2) : 0;
        resumen.TotalDescuentos = cobrados.Sum(p => Math.Max(0, p.SubTotal - p.Total));
        resumen.MontoPagosAnulados = anulados.Sum(p => p.Monto);
        resumen.MontoProductosAnulados = anulacionesProductos.Sum(a => a.Importe);

        resumen.PorMetodo = pagosValidos
            .GroupBy(p => p.MetodoPago?.Nombre ?? "—")
            .Select(g => new LineaResumenDto { Nombre = g.Key, Cantidad = g.Count(), Total = g.Sum(p => p.Monto) })
            .OrderByDescending(l => l.Total)
            .ToList();

        resumen.PorMozo = cobrados
            .GroupBy(p => p.Usuario?.NombreCompleto ?? "—")
            .Select(g => new LineaResumenDto { Nombre = g.Key, Cantidad = g.Count(), Total = g.Sum(p => p.Total) })
            .OrderByDescending(l => l.Total)
            .ToList();

        resumen.PorSalon = cobrados
            .GroupBy(p => p.Mesa?.Salon?.Nombre ?? "Sin salón")
            .Select(g => new LineaResumenDto { Nombre = g.Key, Cantidad = g.Count(), Total = g.Sum(p => p.Total) })
            .OrderByDescending(l => l.Total)
            .ToList();

        resumen.TopProductos = cobrados
            .SelectMany(p => p.DetallesPedidos)
            .Where(d => d.EstadoDetallePedido != EstadoDetallePedido.Cancelado)
            .GroupBy(d => d.Producto?.Nombre ?? "Producto")
            .Select(g => new LineaResumenDto { Nombre = g.Key, Cantidad = g.Sum(d => d.Cantidad), Total = g.Sum(d => d.Cantidad * d.Monto) })
            .OrderByDescending(l => l.Cantidad).ThenByDescending(l => l.Total)
            .Take(10)
            .ToList();

        if (cerrada)
        {
            var arqueos = await context.JornadaArqueos
                .AsNoTracking()
                .Where(a => a.JornadaId == jornadaId)
                .OrderByDescending(a => a.EsEfectivo).ThenBy(a => a.JornadaArqueoId)
                .ToListAsync(ct);
            resumen.Arqueo = arqueos.Select(a => new ArqueoDto
            {
                MetodoPagoId = a.MetodoPagoId,
                MetodoPago = a.MetodoPago,
                EsEfectivo = a.EsEfectivo,
                Ventas = a.Ventas,
                Esperado = a.Esperado,
                Contado = a.Contado,
                Diferencia = a.Diferencia
            }).ToList();
            resumen.DiferenciaTotal = jornada.DiferenciaTotal;
        }
        else
        {
            resumen.Arqueo = await CalcularEsperadoAsync(context, jornada, ct);
        }

        return resumen;
    }

    /// <summary>"B001-00000012 al B001-00000040" (por serie; varias series se separan con " · ").</summary>
    private static string? RangoNumeros(IEnumerable<(string Serie, int Correlativo)> numeros)
    {
        var porSerie = numeros
            .GroupBy(n => n.Serie)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var min = g.Min(n => n.Correlativo);
                var max = g.Max(n => n.Correlativo);
                return min == max
                    ? ComprobanteMapper.FormatearNumero(g.Key, min)
                    : $"{ComprobanteMapper.FormatearNumero(g.Key, min)} al {ComprobanteMapper.FormatearNumero(g.Key, max)}";
            })
            .ToList();
        return porSerie.Count == 0 ? null : string.Join(" · ", porSerie);
    }
}
