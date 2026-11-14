using CaraNegra.Application.Common;
using CaraNegra.Application.Comprobantes;
using CaraNegra.Application.Comprobantes.Queries;
using CaraNegra.Application.Jornadas;
using CaraNegra.Application.Pedidos.DTOs;
using CaraNegra.Application.Reportes.DTOs;
using CaraNegra.Domain.Enums;
using ClosedXML.Excel;

namespace CaraNegra.API.Reportes;

/// <summary>
/// Construye los archivos .xlsx que se descargan desde el panel de Reportes (Fase 7).
/// Se genera todo en memoria con ClosedXML; no se guarda nada en disco del servidor.
/// </summary>
public static class ReportesExcelBuilder
{
    /// <summary>Resumen de ventas + productos más vendidos, en un solo libro con dos hojas.</summary>
    public static byte[] ConstruirResumenVentas(ResumenVentasDto resumen, List<ProductoMasVendidoDto> productos)
    {
        using var libro = new XLWorkbook();

        var hojaResumen = libro.Worksheets.Add("Resumen");
        hojaResumen.Cell(1, 1).Value = "Cara Negra — Resumen de ventas";
        hojaResumen.Cell(1, 1).Style.Font.Bold = true;
        hojaResumen.Cell(1, 1).Style.Font.FontSize = 14;

        hojaResumen.Cell(2, 1).Value = "Desde";
        hojaResumen.Cell(2, 2).Value = resumen.FechaDesde;
        hojaResumen.Cell(2, 2).Style.DateFormat.Format = "dd/MM/yyyy";
        hojaResumen.Cell(3, 1).Value = "Hasta";
        hojaResumen.Cell(3, 2).Value = resumen.FechaHasta;
        hojaResumen.Cell(3, 2).Style.DateFormat.Format = "dd/MM/yyyy";

        var filaKpis = 5;
        void EscribirKpi(string etiqueta, object valor, string? formato = null)
        {
            hojaResumen.Cell(filaKpis, 1).Value = etiqueta;
            hojaResumen.Cell(filaKpis, 1).Style.Font.Bold = true;
            var celdaValor = hojaResumen.Cell(filaKpis, 2);
            if (valor is decimal dec) celdaValor.Value = dec;
            else if (valor is int i) celdaValor.Value = i;
            else celdaValor.Value = valor?.ToString() ?? string.Empty;
            if (formato != null) celdaValor.Style.NumberFormat.Format = formato;
            filaKpis++;
        }

        EscribirKpi("Total ventas (S/)", resumen.TotalVentas, "#,##0.00");
        EscribirKpi("Ticket promedio (S/)", resumen.TicketPromedio, "#,##0.00");
        EscribirKpi("Cantidad de pedidos", resumen.CantidadPedidos);
        EscribirKpi("Pedidos pagados", resumen.CantidadPedidosPagados);
        EscribirKpi("Pedidos cancelados", resumen.CantidadPedidosCancelados);

        filaKpis += 1;
        hojaResumen.Cell(filaKpis, 1).Value = "Ventas por método de pago";
        hojaResumen.Cell(filaKpis, 1).Style.Font.Bold = true;
        filaKpis++;
        hojaResumen.Cell(filaKpis, 1).Value = "Método de pago";
        hojaResumen.Cell(filaKpis, 2).Value = "Cantidad de pagos";
        hojaResumen.Cell(filaKpis, 3).Value = "Total (S/)";
        hojaResumen.Range(filaKpis, 1, filaKpis, 3).Style.Font.Bold = true;
        filaKpis++;
        foreach (var v in resumen.VentasPorMetodoPago)
        {
            hojaResumen.Cell(filaKpis, 1).Value = v.MetodoPagoNombre;
            hojaResumen.Cell(filaKpis, 2).Value = v.CantidadPagos;
            hojaResumen.Cell(filaKpis, 3).Value = v.Total;
            hojaResumen.Cell(filaKpis, 3).Style.NumberFormat.Format = "#,##0.00";
            filaKpis++;
        }

        hojaResumen.Columns().AdjustToContents();

        var hojaProductos = libro.Worksheets.Add("Productos mas vendidos");
        var encabezados = new[] { "Producto", "Categoria", "Cantidad vendida", "Total vendido (S/)" };
        for (var c = 0; c < encabezados.Length; c++)
        {
            hojaProductos.Cell(1, c + 1).Value = encabezados[c];
        }
        hojaProductos.Row(1).Style.Font.Bold = true;

        var filaProd = 2;
        foreach (var p in productos)
        {
            hojaProductos.Cell(filaProd, 1).Value = p.ProductoNombre;
            hojaProductos.Cell(filaProd, 2).Value = p.CategoriaNombre;
            hojaProductos.Cell(filaProd, 3).Value = p.CantidadVendida;
            hojaProductos.Cell(filaProd, 4).Value = p.TotalVendido;
            hojaProductos.Cell(filaProd, 4).Style.NumberFormat.Format = "#,##0.00";
            filaProd++;
        }
        hojaProductos.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        libro.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>Detalle línea por línea de todos los pedidos de una mesa en el rango indicado.</summary>
    public static byte[] ConstruirPedidosPorMesa(List<PedidoDto> pedidos, string mesaNumero)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add(NombreHojaValido($"Mesa {mesaNumero}"));

        hoja.Cell(1, 1).Value = $"Cara Negra — Pedidos de la mesa {mesaNumero}";
        hoja.Cell(1, 1).Style.Font.Bold = true;
        hoja.Cell(1, 1).Style.Font.FontSize = 14;

        var encabezados = new[] { "Pedido #", "Fecha", "Estado del pedido", "Producto", "Cantidad", "Precio unitario (S/)", "Total línea (S/)", "Notas" };
        for (var c = 0; c < encabezados.Length; c++)
        {
            hoja.Cell(3, c + 1).Value = encabezados[c];
        }
        hoja.Row(3).Style.Font.Bold = true;

        var fila = 4;
        foreach (var pedido in pedidos.OrderBy(p => p.CreadoEn))
        {
            foreach (var d in pedido.Detalles)
            {
                hoja.Cell(fila, 1).Value = pedido.PedidoId;
                hoja.Cell(fila, 2).Value = pedido.CreadoEn;
                hoja.Cell(fila, 2).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
                hoja.Cell(fila, 3).Value = pedido.EstadoPedido.ToString();
                hoja.Cell(fila, 4).Value = d.ProductoNombre;
                hoja.Cell(fila, 5).Value = d.Cantidad;
                hoja.Cell(fila, 6).Value = d.Monto;
                hoja.Cell(fila, 6).Style.NumberFormat.Format = "#,##0.00";
                hoja.Cell(fila, 7).Value = d.Monto * d.Cantidad;
                hoja.Cell(fila, 7).Style.NumberFormat.Format = "#,##0.00";
                hoja.Cell(fila, 8).Value = d.Notas ?? string.Empty;
                fila++;
            }
        }

        hoja.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        libro.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Comprobantes electrónicos (boletas y facturas) del rango indicado, uno por fila, con su
    /// estado frente a SUNAT. Igual que en la pantalla de Comprobantes, los totales solo suman
    /// los válidos: los rechazados, con error de envío o anulados salen en gris y no cuentan.
    /// </summary>
    public static byte[] ConstruirComprobantes(ComprobantesListadoDto listado, DateTime fechaDesde, DateTime fechaHasta)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Comprobantes");

        hoja.Cell(1, 1).Value = "Comprobantes electrónicos";
        hoja.Cell(1, 1).Style.Font.Bold = true;
        hoja.Cell(1, 1).Style.Font.FontSize = 14;
        hoja.Cell(2, 1).Value = fechaDesde.Date == fechaHasta.Date
            ? $"Fecha: {fechaDesde:dd/MM/yyyy}"
            : $"Del {fechaDesde:dd/MM/yyyy} al {fechaHasta:dd/MM/yyyy}";

        const int filaEncabezado = 4;
        var encabezados = new[]
        {
            "Fecha", "Hora", "Tipo", "Número", "Documento cliente", "Cliente",
            "Op. gravada (S/)", "IGV (S/)", "Total (S/)", "Estado SUNAT", "Mensaje SUNAT"
        };
        for (var c = 0; c < encabezados.Length; c++)
        {
            hoja.Cell(filaEncabezado, c + 1).Value = encabezados[c];
        }
        hoja.Row(filaEncabezado).Style.Font.Bold = true;

        var fila = filaEncabezado + 1;
        foreach (var c in listado.Items.OrderBy(x => x.FechaEmision))
        {
            // FechaEmision se guarda en UTC: se muestra en hora de Lima, como en pantalla.
            var horaLima = PeruDateRangeHelper.ToHoraPeru(c.FechaEmision);
            hoja.Cell(fila, 1).Value = horaLima.Date;
            hoja.Cell(fila, 1).Style.DateFormat.Format = "dd/MM/yyyy";
            hoja.Cell(fila, 2).Value = horaLima.ToString("HH:mm");
            hoja.Cell(fila, 3).Value = c.TipoComprobante == TipoComprobante.Factura ? "Factura" : "Boleta";
            hoja.Cell(fila, 4).Value = c.Numero;
            hoja.Cell(fila, 5).Value = c.ClienteNumeroDocumento ?? string.Empty;
            hoja.Cell(fila, 6).Value = c.ClienteNombre;
            hoja.Cell(fila, 7).Value = c.OpGravada;
            hoja.Cell(fila, 8).Value = c.Igv;
            hoja.Cell(fila, 9).Value = c.Total;
            hoja.Range(fila, 7, fila, 9).Style.NumberFormat.Format = "#,##0.00";
            hoja.Cell(fila, 10).Value = TextoEstado(c.Estado);
            hoja.Cell(fila, 11).Value = c.MensajeSunat ?? string.Empty;

            if (ComprobanteEnvio.NoEsValido(c.Estado))
            {
                hoja.Range(fila, 1, fila, encabezados.Length).Style.Font.FontColor = XLColor.Gray;
            }
            fila++;
        }
        var ultimaFilaDatos = fila - 1;

        // Totales (solo comprobantes válidos, los mismos que muestra la pantalla).
        fila++;
        hoja.Cell(fila, 6).Value = "Total (solo válidos)";
        hoja.Cell(fila, 7).Value = listado.TotalOpGravada;
        hoja.Cell(fila, 8).Value = listado.TotalIgv;
        hoja.Cell(fila, 9).Value = listado.TotalVendido;
        hoja.Range(fila, 7, fila, 9).Style.NumberFormat.Format = "#,##0.00";
        hoja.Range(fila, 6, fila, 9).Style.Font.Bold = true;
        fila++;
        hoja.Cell(fila, 6).Value = "Boletas";
        hoja.Cell(fila, 7).Value = listado.CantidadBoletas;
        fila++;
        hoja.Cell(fila, 6).Value = "Facturas";
        hoja.Cell(fila, 7).Value = listado.CantidadFacturas;

        if (ultimaFilaDatos > filaEncabezado)
        {
            hoja.Range(filaEncabezado, 1, ultimaFilaDatos, encabezados.Length).SetAutoFilter();
        }
        hoja.SheetView.FreezeRows(filaEncabezado);

        // Se ajusta desde la fila de encabezados para que el título no ensanche la primera columna.
        hoja.Columns().AdjustToContents(filaEncabezado, fila);
        if (hoja.Column(11).Width > 60) hoja.Column(11).Width = 60;

        using var stream = new MemoryStream();
        libro.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Historial de pedidos del rango (Pedidos del día): todos los pedidos, cobrados, abiertos y
    /// anulados, con sus medios de pago y comprobante. Igual que en pantalla, lo cobrado no suma
    /// los pedidos anulados, que salen en gris.
    /// </summary>
    public static byte[] ConstruirHistorialPedidos(PedidosJornadaDto datos, DateTime fechaDesde, DateTime fechaHasta)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Pedidos");

        hoja.Cell(1, 1).Value = "Historial de pedidos";
        hoja.Cell(1, 1).Style.Font.Bold = true;
        hoja.Cell(1, 1).Style.Font.FontSize = 14;
        hoja.Cell(2, 1).Value = fechaDesde.Date == fechaHasta.Date
            ? $"Jornada del {fechaDesde:dd/MM/yyyy}"
            : $"Jornadas del {fechaDesde:dd/MM/yyyy} al {fechaHasta:dd/MM/yyyy}";

        const int filaEncabezado = 4;
        var encabezados = new[]
        {
            "Día (jornada)", "Hora", "Pedido #", "Salón", "Mesa", "Mozo", "Platos", "Estado",
            "Total (S/)", "Cobrado (S/)", "Medios de pago", "Comprobante", "Comentario"
        };
        for (var c = 0; c < encabezados.Length; c++)
        {
            hoja.Cell(filaEncabezado, c + 1).Value = encabezados[c];
        }
        hoja.Row(filaEncabezado).Style.Font.Bold = true;

        var fila = filaEncabezado + 1;
        foreach (var p in datos.Pedidos.OrderBy(x => x.CreadoEn))
        {
            var horaLima = PeruDateRangeHelper.ToHoraPeru(p.CreadoEn);
            hoja.Cell(fila, 1).Value = (p.FechaOperativa ?? horaLima).Date;
            hoja.Cell(fila, 1).Style.DateFormat.Format = "dd/MM/yyyy";
            hoja.Cell(fila, 2).Value = horaLima.ToString("HH:mm");
            hoja.Cell(fila, 3).Value = p.PedidoId;
            hoja.Cell(fila, 4).Value = p.SalonNombre;
            hoja.Cell(fila, 5).Value = p.MesaNumero;
            hoja.Cell(fila, 6).Value = p.Mozo;
            hoja.Cell(fila, 7).Value = p.CantidadPlatos;
            hoja.Cell(fila, 8).Value = TextoEstadoPedido(p.Estado);
            hoja.Cell(fila, 9).Value = p.Total;
            hoja.Cell(fila, 10).Value = p.Estado == EstadoPedido.Cancelado ? 0m : p.Pagado;
            hoja.Range(fila, 9, fila, 10).Style.NumberFormat.Format = "#,##0.00";
            hoja.Cell(fila, 11).Value = string.Join(" + ", p.MetodosPago);
            hoja.Cell(fila, 12).Value = p.Comprobante?.Numero ?? string.Empty;
            hoja.Cell(fila, 13).Value = p.Comentario ?? string.Empty;

            if (p.Estado == EstadoPedido.Cancelado)
            {
                hoja.Range(fila, 1, fila, encabezados.Length).Style.Font.FontColor = XLColor.Gray;
            }
            fila++;
        }
        var ultimaFilaDatos = fila - 1;

        // Totales, con las mismas reglas que la pantalla.
        var cobrados = datos.Pedidos.Count(p => p.Estado == EstadoPedido.Entregado);
        var anulados = datos.Pedidos.Count(p => p.Estado == EstadoPedido.Cancelado);
        var abiertos = datos.Pedidos.Count - cobrados - anulados;
        var totalCobrado = datos.Pedidos.Where(p => p.Estado != EstadoPedido.Cancelado).Sum(p => p.Pagado);

        fila++;
        hoja.Cell(fila, 8).Value = "Total cobrado";
        hoja.Cell(fila, 10).Value = totalCobrado;
        hoja.Cell(fila, 10).Style.NumberFormat.Format = "#,##0.00";
        hoja.Range(fila, 8, fila, 10).Style.Font.Bold = true;
        fila++;
        hoja.Cell(fila, 8).Value = "Pedidos";
        hoja.Cell(fila, 10).Value = datos.Pedidos.Count;
        fila++;
        hoja.Cell(fila, 8).Value = "Cobrados";
        hoja.Cell(fila, 10).Value = cobrados;
        fila++;
        hoja.Cell(fila, 8).Value = "Abiertos";
        hoja.Cell(fila, 10).Value = abiertos;
        fila++;
        hoja.Cell(fila, 8).Value = "Anulados";
        hoja.Cell(fila, 10).Value = anulados;

        if (ultimaFilaDatos > filaEncabezado)
        {
            hoja.Range(filaEncabezado, 1, ultimaFilaDatos, encabezados.Length).SetAutoFilter();
        }
        hoja.SheetView.FreezeRows(filaEncabezado);

        hoja.Columns().AdjustToContents(filaEncabezado, fila);
        if (hoja.Column(13).Width > 60) hoja.Column(13).Width = 60;

        using var stream = new MemoryStream();
        libro.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>Mismos grupos que la pantalla Pedidos del día.</summary>
    private static string TextoEstadoPedido(EstadoPedido estado) => estado switch
    {
        EstadoPedido.Entregado => "Cobrado",
        EstadoPedido.Cancelado => "Anulado",
        _ => "Abierto"
    };

    /// <summary>Mismos textos que la pantalla de Comprobantes (utils/estadoComprobante.js).</summary>
    private static string TextoEstado(EstadoComprobante estado) => estado switch
    {
        EstadoComprobante.AceptadoSimulado => "Simulado (demo)",
        EstadoComprobante.Aceptado => "Aceptado por SUNAT",
        EstadoComprobante.Pendiente => "Esperando a SUNAT",
        EstadoComprobante.PorEnviar => "Sin enviar",
        EstadoComprobante.Rechazado => "Rechazado por SUNAT",
        EstadoComprobante.ErrorEnvio => "No aceptado por el proveedor",
        EstadoComprobante.Anulado => "Anulado",
        _ => estado.ToString()
    };

    // Excel no permite \ / ? * [ ] en el nombre de una hoja, ni más de 31 caracteres. El
    // número/código de mesa lo escribe el usuario libremente (Fase 7: ahora es texto, no un
    // int), así que hay que sanearlo antes de usarlo como nombre de hoja.
    private static readonly char[] CaracteresInvalidosHoja = { '\\', '/', '?', '*', '[', ']', ':' };

    private static string NombreHojaValido(string nombreDeseado)
    {
        var limpio = new string(nombreDeseado.Select(c => CaracteresInvalidosHoja.Contains(c) ? '-' : c).ToArray());
        return limpio.Length <= 31 ? limpio : limpio[..31];
    }
}
