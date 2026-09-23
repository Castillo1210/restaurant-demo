using CaraNegra.Application.Pedidos.DTOs;
using CaraNegra.Application.Reportes.DTOs;
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
