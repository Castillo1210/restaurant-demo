using System.Globalization;
using System.Text.Json.Serialization;
using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Domain.Enums;

namespace CaraNegra.API.FacturacionElectronica;

/// <summary>
/// Arma el JSON de APISUNAT (POST /api/v3/documents) a partir de un comprobante.
///
/// Los precios de la carta INCLUYEN IGV, pero APISUNAT pide el valor unitario SIN IGV
/// (hasta 6 decimales) y calcula el IGV por línea. Si el pedido tuvo descuento, se reparte entre
/// las líneas como "descuento por ítem" (código 00: afecta la base imponible, monto sin IGV),
/// ajustando la última línea para que el total enviado coincida con lo cobrado.
/// </summary>
public static class ApiSunatPayload
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static ApiSunatDocumento Construir(SolicitudComprobante s)
    {
        var factor = 1 + s.IgvPorcentaje / 100m;
        var items = s.Items.Where(i => i.Cantidad > 0).ToList();
        if (items.Count == 0)
            throw new InvalidOperationException("El comprobante no tiene productos.");

        var valores = items.Select(i => Math.Round(i.PrecioUnitario / factor, 6, MidpointRounding.AwayFromZero)).ToArray();
        var brutos = items.Select(i => i.Cantidad * i.PrecioUnitario).ToArray();
        var subtotal = brutos.Sum();

        // Descuento (con IGV) = lo que suma la carta - lo que se cobró. Se reparte SIN IGV.
        var descuentos = new decimal[items.Count];
        var descuentoConIgv = Math.Max(0, subtotal - s.Total);
        if (descuentoConIgv > 0 && subtotal > 0)
        {
            var descuentoBase = Math.Round(descuentoConIgv / factor, 2, MidpointRounding.AwayFromZero);
            decimal asignado = 0;
            for (var k = 0; k < items.Count - 1; k++)
            {
                descuentos[k] = Math.Round(descuentoBase * brutos[k] / subtotal, 2, MidpointRounding.AwayFromZero);
                asignado += descuentos[k];
            }
            descuentos[^1] = descuentoBase - asignado;

            AjustarCentimos(items, valores, descuentos, factor, s.Total);
        }

        var (tipoDoc, numeroDoc, denominacion) = Cliente(s);

        return new ApiSunatDocumento
        {
            Documento = s.Tipo == TipoComprobante.Factura ? "factura" : "boleta",
            Serie = s.Serie,
            Numero = s.Numero,
            FechaDeEmision = s.FechaEmisionLocal.ToString("yyyy-MM-dd", Inv),
            HoraDeEmision = s.FechaEmisionLocal.ToString("HH:mm:ss", Inv),
            Moneda = "PEN",
            TipoOperacion = "0101",
            ClienteTipoDeDocumento = tipoDoc,
            ClienteNumeroDeDocumento = numeroDoc,
            ClienteDenominacion = denominacion,
            ClienteDireccion = string.IsNullOrWhiteSpace(s.ClienteDireccion) ? "-" : s.ClienteDireccion.Trim(),
            Items = items.Select((it, k) => new ApiSunatItem
            {
                UnidadDeMedida = "NIU",
                Descripcion = it.Descripcion.Length > 250 ? it.Descripcion[..250] : it.Descripcion,
                Cantidad = it.Cantidad.ToString(Inv),
                ValorUnitario = valores[k].ToString("0.######", Inv),
                PorcentajeIgv = s.IgvPorcentaje.ToString("0.00", Inv),
                CodigoTipoAfectacionIgv = "10",
                NombreTributo = "IGV",
                Descuentos = descuentos[k] > 0
                    ? [new ApiSunatDescuento { Monto = descuentos[k].ToString("0.00", Inv), Codigo = "00" }]
                    : null
            }).ToList(),
            Total = TotalLineas(items, valores, descuentos, factor).ToString("0.00", Inv)
        };
    }

    /// <summary>
    /// Por el redondeo de cada línea, el total puede quedar a 1 céntimo de lo cobrado. Se mueve el
    /// descuento de a 0.01 en la línea que más acerque el total, hasta que cuadre (casi siempre).
    /// Si no se puede cuadrar exacto, queda a ±0.01 (tolerancia que acepta SUNAT).
    /// </summary>
    private static void AjustarCentimos(List<ItemSolicitud> items, decimal[] valores, decimal[] descuentos,
        decimal factor, decimal objetivo)
    {
        var maximos = items.Select((it, k) => Math.Round(it.Cantidad * valores[k], 2)).ToArray();

        for (var vuelta = 0; vuelta < 30; vuelta++)
        {
            var diferencia = objetivo - TotalLineas(items, valores, descuentos, factor);
            if (diferencia == 0) return;

            var mejorLinea = -1;
            var mejorPaso = 0m;
            var mejorDif = Math.Abs(diferencia);
            for (var k = 0; k < items.Count; k++)
            {
                foreach (var paso in new[] { 0.01m, -0.01m, 0.02m, -0.02m })
                {
                    var nuevo = descuentos[k] + paso;
                    if (nuevo < 0 || nuevo > maximos[k]) continue;
                    descuentos[k] = nuevo;
                    var dif = Math.Abs(objetivo - TotalLineas(items, valores, descuentos, factor));
                    descuentos[k] = nuevo - paso;
                    if (dif < mejorDif) { mejorDif = dif; mejorLinea = k; mejorPaso = paso; }
                }
            }

            if (mejorLinea < 0)
            {
                // Ningún paso simple mejora: probar pares de líneas (una sube, otra baja).
                for (var a = 0; a < items.Count && mejorLinea < 0; a++)
                for (var b = 0; b < items.Count && mejorLinea < 0; b++)
                {
                    if (a == b) continue;
                    var na = descuentos[a] + 0.01m;
                    var nb = descuentos[b] - 0.01m;
                    if (na > maximos[a] || nb < 0) continue;
                    descuentos[a] = na; descuentos[b] = nb;
                    if (Math.Abs(objetivo - TotalLineas(items, valores, descuentos, factor)) < Math.Abs(diferencia))
                        mejorLinea = int.MaxValue; // aplicado
                    else { descuentos[a] = na - 0.01m; descuentos[b] = nb + 0.01m; }
                }
                if (mejorLinea < 0) return;
                continue;
            }

            descuentos[mejorLinea] += mejorPaso;
        }
    }

    /// <summary>Total como lo calcula el comprobante: Σ redondeo((cantidad × valor − descuento) × (1 + IGV)).</summary>
    public static decimal TotalLineas(IReadOnlyList<ItemSolicitud> items, decimal[] valores, decimal[] descuentos, decimal factor)
    {
        decimal total = 0;
        for (var k = 0; k < items.Count; k++)
            total += Math.Round((items[k].Cantidad * valores[k] - descuentos[k]) * factor, 2, MidpointRounding.AwayFromZero);
        return total;
    }

    /// <summary>
    /// Catálogo 06 de SUNAT: 1 = DNI, 4 = carné de extranjería, 6 = RUC.
    /// Boleta sin documento: como indica APISUNAT, DNI "99999999" y "CLIENTE VARIOS".
    /// </summary>
    private static (string tipo, string numero, string denominacion) Cliente(SolicitudComprobante s)
    {
        var nombre = string.IsNullOrWhiteSpace(s.ClienteNombre) ? "CLIENTE VARIOS" : s.ClienteNombre.Trim();
        var numero = s.ClienteNumeroDocumento?.Trim();

        return s.ClienteTipoDocumento switch
        {
            TipoDocumentoIdentidad.RUC when !string.IsNullOrEmpty(numero) => ("6", numero, nombre),
            TipoDocumentoIdentidad.DNI when !string.IsNullOrEmpty(numero) => ("1", numero, nombre),
            TipoDocumentoIdentidad.CarnetExtranjeria when !string.IsNullOrEmpty(numero) => ("4", numero, nombre),
            _ => ("1", "99999999", nombre is "CLIENTES VARIOS" ? "CLIENTE VARIOS" : nombre)
        };
    }
}

public class ApiSunatDocumento
{
    [JsonPropertyName("documento")] public string Documento { get; set; } = "boleta";
    [JsonPropertyName("serie")] public string Serie { get; set; } = string.Empty;
    [JsonPropertyName("numero")] public int Numero { get; set; }
    [JsonPropertyName("fecha_de_emision")] public string FechaDeEmision { get; set; } = string.Empty;
    [JsonPropertyName("hora_de_emision")] public string HoraDeEmision { get; set; } = string.Empty;
    [JsonPropertyName("moneda")] public string Moneda { get; set; } = "PEN";
    [JsonPropertyName("tipo_operacion")] public string TipoOperacion { get; set; } = "0101";
    [JsonPropertyName("cliente_tipo_de_documento")] public string ClienteTipoDeDocumento { get; set; } = "1";
    [JsonPropertyName("cliente_numero_de_documento")] public string ClienteNumeroDeDocumento { get; set; } = string.Empty;
    [JsonPropertyName("cliente_denominacion")] public string ClienteDenominacion { get; set; } = string.Empty;
    [JsonPropertyName("cliente_direccion")] public string ClienteDireccion { get; set; } = "-";
    [JsonPropertyName("items")] public List<ApiSunatItem> Items { get; set; } = new();
    [JsonPropertyName("total")] public string Total { get; set; } = "0.00";
}

public class ApiSunatItem
{
    [JsonPropertyName("unidad_de_medida")] public string UnidadDeMedida { get; set; } = "NIU";
    [JsonPropertyName("descripcion")] public string Descripcion { get; set; } = string.Empty;
    [JsonPropertyName("cantidad")] public string Cantidad { get; set; } = "1";
    [JsonPropertyName("valor_unitario")] public string ValorUnitario { get; set; } = "0";
    [JsonPropertyName("porcentaje_igv")] public string PorcentajeIgv { get; set; } = "18.00";
    [JsonPropertyName("codigo_tipo_afectacion_igv")] public string CodigoTipoAfectacionIgv { get; set; } = "10";
    [JsonPropertyName("nombre_tributo")] public string NombreTributo { get; set; } = "IGV";

    [JsonPropertyName("descuentos")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ApiSunatDescuento>? Descuentos { get; set; }
}

public class ApiSunatDescuento
{
    [JsonPropertyName("monto")] public string Monto { get; set; } = "0.00";
    [JsonPropertyName("codigo")] public string Codigo { get; set; } = "00";
}
