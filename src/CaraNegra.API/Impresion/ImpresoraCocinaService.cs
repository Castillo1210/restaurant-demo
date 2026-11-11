using System.Net.Sockets;
using System.Text;
using CaraNegra.API.Hubs;
using CaraNegra.Application.Common.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace CaraNegra.API.Impresion;

/// <summary>
/// Envía comandas a una impresora térmica ESC/POS conectada por red (típicamente puerto
/// 9100/JetDirect), abriendo una conexión TCP simple y mandando el ticket.
///
/// Formato compacto para no gastar papel: la mesa en letra grande (es lo que cocina busca), el
/// tipo de ticket (ADICIONAL, ANULADO…) en doble alto, mozo y hora en una sola línea, y los
/// productos seguidos, sin líneas en blanco entre ellos, en el tamaño configurado
/// (ImpresoraCocina:TamanoProductos). Corte de papel al final (sin cortador se ignora).
/// </summary>
public class ImpresoraCocinaService : IImpresoraCocinaService
{
    private readonly ImpresoraCocinaOptions _options;
    private readonly ILogger<ImpresoraCocinaService> _logger;
    private readonly IHubContext<PedidosHub> _hub;

    // Una comanda a la vez: muchas ticketeras de red atienden una sola conexión en el puerto 9100,
    // y dos mozos enviando en el mismo segundo hacían que la segunda se perdiera.
    private readonly SemaphoreSlim _turno = new(1, 1);

    public ImpresoraCocinaService(IOptions<ImpresoraCocinaOptions> options, ILogger<ImpresoraCocinaService> logger,
        IHubContext<PedidosHub> hub)
    {
        _options = options.Value;
        _logger = logger;
        _hub = hub;
    }

    public async Task<ResultadoImpresion> ImprimirComandaAsync(ComandaCocina comanda, CancellationToken cancellationToken = default)
    {
        // Pedido del administrador o del cajero: la comanda la imprime su propio equipo en su
        // ticketera, igual que la boleta. Aquí no se manda nada a la ticketera de cocina.
        if (comanda.ParaCaja)
        {
            _logger.LogInformation("Comanda del pedido {PedidoId}: la imprime en caja el equipo que envió el pedido.", comanda.PedidoId);
            return ResultadoImpresion.Impresa;
        }

        if (!_options.Habilitada || string.IsNullOrWhiteSpace(_options.Ip))
        {
            _logger.LogInformation(
                "Impresora de cocina deshabilitada o sin configurar; se omite la impresión de la comanda del pedido {PedidoId}.",
                comanda.PedidoId);
            return ResultadoImpresion.Deshabilitada;
        }

        var turnoTomado = false;
        try
        {
            var ticket = ConstruirTicket(comanda);

            // Esperar el turno (otra comanda imprimiéndose), con un tope para no colgar la petición.
            turnoTomado = await _turno.WaitAsync(TimeSpan.FromMilliseconds(_options.TimeoutMs * 2), cancellationToken);
            if (!turnoTomado)
                throw new TimeoutException("La impresora sigue ocupada con otra comanda.");

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(_options.TimeoutMs));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            using var cliente = new TcpClient();
            await cliente.ConnectAsync(_options.Ip, _options.Puerto, linkedCts.Token);

            await using var stream = cliente.GetStream();
            await stream.WriteAsync(ticket, linkedCts.Token);
            await stream.FlushAsync(linkedCts.Token);
            return ResultadoImpresion.Impresa;
        }
        catch (Exception ex)
        {
            // Nunca se relanza: la impresora apagada/desconectada/mal configurada no debe
            // impedir que el mozo tome pedidos ni que caja cobre. Pero se avisa a las pantallas.
            _logger.LogWarning(ex,
                "No se pudo imprimir la comanda del pedido {PedidoId} en la impresora de cocina ({Ip}:{Puerto}).",
                comanda.PedidoId, _options.Ip, _options.Puerto);
        }
        finally
        {
            // Se suelta el turno ANTES de avisar a las pantallas: el aviso no debe frenar a la siguiente comanda.
            if (turnoTomado) _turno.Release();
        }

        await AvisarFalloAsync(comanda);
        return ResultadoImpresion.Fallo;
    }

    /// <summary>
    /// Aviso en vivo a mozos, caja, admin y cocina: "esta comanda NO salió". La reimpresión a pedido
    /// no avisa aquí (quien la pidió recibe el error directamente).
    /// </summary>
    private async Task AvisarFalloAsync(ComandaCocina comanda)
    {
        if (comanda.EsReimpresion) return;
        try
        {
            var tipo = comanda.EsAnulacion ? (comanda.EsPedidoCompleto ? "Pedido anulado" : "Anulación")
                : comanda.EsCambioComentario ? "Nota del pedido"
                : comanda.EsAdicional ? "Adicional"
                : "Comanda";
            // Primeros platos de la comanda ("2x Cuy, 1x Chicha…") para que el mozo sepa cuál no salió.
            var platos = string.Join(", ", comanda.Items.Take(3).Select(i => $"{i.Cantidad}x {i.ProductoNombre}"));
            if (comanda.Items.Count > 3) platos += $" y {comanda.Items.Count - 3} más";
            var evento = new
            {
                pedidoId = comanda.PedidoId,
                salonNombre = comanda.SalonNombre,
                mesaNumero = comanda.MesaNumero,
                tipo,
                platos,
                // Reimprimir saca el pedido COMPLETO: solo tiene sentido si lo que falló fue la comanda
                // completa. Si falló un adicional, una nota o una anulación, reimprimir todo confundiría
                // a cocina (prepararía de nuevo lo que ya salió).
                puedeReimprimir = tipo == "Comanda"
            };
            await _hub.Clients.Groups("Mozo", "Cajero", "Admin", "Cocina").SendAsync("ImpresionFallida", evento);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo avisar a las pantallas que la comanda del pedido {PedidoId} no salió.",
                comanda.PedidoId);
        }
    }

    /// <summary>
    /// Texto de la comanda tal como sale en papel (las líneas en letra grande van más cortas,
    /// igual que en la ticketera), sin los códigos de control de la impresora.
    /// </summary>
    public string PrevisualizarComanda(ComandaCocina comanda)
    {
        var sb = new StringBuilder();
        var columnas = Columnas;
        foreach (var linea in ArmarComanda(comanda))
        {
            var ancho = linea.Tam == Tamano.Grande ? columnas / 2 : columnas;
            var texto = NormalizarAscii(linea.Texto);
            sb.Append(linea.Centrada ? texto.PadLeft((ancho + texto.Length) / 2) : texto).Append('\n');
            if (linea.EspacioDespues) sb.Append('\n');
        }
        return sb.ToString().TrimEnd('\n');
    }

    // ───────────── Armado de la comanda ─────────────

    private enum Tamano { Normal, Alto, Grande }

    /// <summary>Una línea del ticket ya cortada al ancho que le corresponde según su tamaño.</summary>
    private sealed record Linea(string Texto, Tamano Tam = Tamano.Normal, bool Negrita = false,
        bool Centrada = false, bool EspacioDespues = false);

    /// <summary>Caracteres por línea en letra normal (80 mm: 42-48; 58 mm: 32).</summary>
    private int Columnas => Math.Clamp(_options.Columnas, 24, 64);

    private Tamano TamanoProductos => (_options.TamanoProductos ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "grande" => Tamano.Grande,
        "normal" => Tamano.Normal,
        _ => Tamano.Alto
    };

    private List<Linea> ArmarComanda(ComandaCocina comanda)
    {
        var columnas = Columnas;
        var lineas = new List<Linea>();
        var separador = new string('=', columnas);

        // Tipo de ticket en doble alto (no doble ancho): se distingue sin ocupar tanto papel.
        // El nombre del restaurante no se imprime: el ticket no sale de la cocina.
        if (comanda.EsReimpresion)
            lineas.Add(new Linea("** REIMPRESION **", Tamano.Alto, Negrita: true, Centrada: true));
        if (comanda.EsCambioComentario)
            lineas.Add(new Linea("** NOTA DEL PEDIDO **", Tamano.Alto, Negrita: true, Centrada: true));
        else if (comanda.EsAnulacion)
            lineas.Add(new Linea(comanda.EsPedidoCompleto ? "** PEDIDO ANULADO **" : "** ANULADO **", Tamano.Alto, Negrita: true, Centrada: true));
        else if (comanda.EsAdicional)
            lineas.Add(new Linea("** ADICIONAL **", Tamano.Alto, Negrita: true, Centrada: true));

        // La numeración de mesas es por salón: la mesa se identifica con la primera palabra del
        // salón + su número ("TERRAZA 1"), igual que en las pantallas. Si el salón tiene un
        // nombre compuesto ("Zona de piscina") se imprime además completo debajo.
        var salon = SinTildes(comanda.SalonNombre ?? string.Empty).Trim();
        var palabrasSalon = salon.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var etiquetaMesa = palabrasSalon.Length > 0
            ? $"{palabrasSalon[0].ToUpperInvariant()} {comanda.MesaNumero}"
            : $"MESA {comanda.MesaNumero}";
        foreach (var parte in Envolver(etiquetaMesa, columnas / 2, string.Empty))
            lineas.Add(new Linea(parte, Tamano.Grande, Negrita: true, Centrada: true));
        if (palabrasSalon.Length > 1)
            lineas.Add(new Linea($"({salon})", Centrada: true));

        // Mozo (o quien anuló) y la hora en UNA línea; sin número de pedido (cocina se guía por la mesa).
        // CreadoEn se guarda en UTC: se imprime en hora de Lima.
        var hora = CaraNegra.Application.Common.PeruDateRangeHelper.ToHoraPeru(comanda.CreadoEn);
        var textoHora = $"{hora:dd/MM HH:mm}";
        var rotuloPersona = comanda.EsAnulacion ? "Anulo" : comanda.EsCambioComentario ? "Por" : "Mozo";
        var persona = $"{rotuloPersona}: {comanda.MozoNombre}";
        if (persona.Length + 1 + textoHora.Length <= columnas)
            lineas.Add(new Linea(Columnas2(persona, textoHora, columnas)));
        else
        {
            foreach (var parte in Envolver(persona, columnas, new string(' ', rotuloPersona.Length + 2)))
                lineas.Add(new Linea(parte));
            lineas.Add(new Linea(textoHora));
        }
        if (comanda.EsAnulacion && !string.IsNullOrWhiteSpace(comanda.Motivo))
            foreach (var parte in Envolver($"Motivo: {comanda.Motivo!.Trim()}", columnas, "        "))
                lineas.Add(new Linea(parte, Negrita: true));
        lineas.Add(new Linea(separador));

        // Comentario general del pedido ("todo para llevar"): en doble alto ANTES de los platos,
        // para que sea lo primero que lee cocina. En una anulación no aplica.
        var comentario = comanda.EsAnulacion ? null : comanda.Comentario?.Trim();
        if (!string.IsNullOrEmpty(comentario))
        {
            var partesComentario = Envolver($"NOTA: {comentario}", columnas, "      ");
            for (var k = 0; k < partesComentario.Count; k++)
                lineas.Add(new Linea(partesComentario[k], Tamano.Alto, Negrita: true));
            lineas.Add(new Linea(separador));
        }
        if (comanda.EsCambioComentario)
        {
            if (string.IsNullOrEmpty(comentario))
                lineas.Add(new Linea("(sin comentario)", Centrada: true));
            return lineas;
        }

        // Productos seguidos, sin líneas en blanco entre ellos (ahorra papel). En una anulación la
        // cocina solo tiene que reconocer los platos → siempre letra normal.
        var tam = comanda.EsAnulacion ? Tamano.Normal : TamanoProductos;
        var ancho = tam == Tamano.Grande ? columnas / 2 : columnas;
        foreach (var item in comanda.Items)
        {
            var prefijo = $"{item.Cantidad} x ";
            foreach (var parte in Envolver(prefijo + item.ProductoNombre, ancho, new string(' ', prefijo.Length)))
                lineas.Add(new Linea(parte, tam, Negrita: true));

            // La nota del plato ("sin cebolla") justo debajo, del mismo tamaño y marcada con ">>".
            if (!string.IsNullOrWhiteSpace(item.Notas))
                foreach (var parte in Envolver($">> {item.Notas!.Trim()}", ancho, "   "))
                    lineas.Add(new Linea(parte, tam, Negrita: true));
        }

        lineas.Add(new Linea(separador));
        if (comanda.EsAnulacion)
        {
            lineas.Add(new Linea("NO PREPARAR", Tamano.Alto, Negrita: true, Centrada: true));
            return lineas;
        }
        var totalUnidades = comanda.Items.Sum(i => i.Cantidad);
        lineas.Add(new Linea($"{totalUnidades} unidad{(totalUnidades == 1 ? "" : "es")} · {comanda.Items.Count} producto{(comanda.Items.Count == 1 ? "" : "s")}",
            Centrada: true));
        return lineas;
    }

    private byte[] ConstruirTicket(ComandaCocina comanda)
    {
        var b = new List<byte>(1024) { 0x1B, 0x40 };          // ESC @: inicializar
        foreach (var linea in ArmarComanda(comanda))
        {
            b.AddRange(new byte[] { 0x1B, 0x61, (byte)(linea.Centrada ? 1 : 0) });   // ESC a: alineación
            b.AddRange(new byte[] { 0x1B, 0x45, (byte)(linea.Negrita ? 1 : 0) });    // ESC E: negrita
            b.AddRange(new byte[] { 0x1D, 0x21, linea.Tam switch                       // GS !: tamaño
            {
                Tamano.Grande => (byte)0x11,   // doble ancho + doble alto
                Tamano.Alto => (byte)0x01,     // doble alto
                _ => (byte)0x00
            } });
            b.AddRange(Encoding.ASCII.GetBytes(NormalizarAscii(linea.Texto)));
            b.Add(0x0A);
            if (linea.EspacioDespues)
            {
                b.AddRange(new byte[] { 0x1D, 0x21, 0x00 });  // el espacio entre productos en tamaño normal
                b.Add(0x0A);
            }
        }
        b.AddRange(new byte[] { 0x1D, 0x21, 0x00, 0x1B, 0x45, 0x00, 0x1B, 0x61, 0x00 }); // volver a normal
        b.AddRange(new byte[] { 0x1B, 0x64, 0x04 });          // ESC d 4: avanzar 4 líneas
        b.AddRange(new byte[] { 0x1D, 0x56, 0x42, 0x00 });    // GS V B 0: cortar (si tiene cortador)
        return b.ToArray();
    }

    // ───────────── Utilidades de texto ─────────────

    /// <summary>Texto a la izquierda y a la derecha en la misma línea.</summary>
    private static string Columnas2(string izquierda, string derecha, int ancho)
    {
        var espacios = ancho - izquierda.Length - derecha.Length;
        return espacios >= 1 ? izquierda + new string(' ', espacios) + derecha : $"{izquierda} {derecha}";
    }

    /// <summary>
    /// Corta un texto en líneas de como máximo <paramref name="ancho"/> caracteres, por palabras
    /// (la impresora cortaría en medio de una palabra). Las líneas siguientes llevan sangría.
    /// </summary>
    internal static List<string> Envolver(string texto, int ancho, string sangria)
    {
        var lineas = new List<string>();
        ancho = Math.Max(ancho, sangria.Length + 4);
        var actual = new StringBuilder();
        foreach (var palabraOriginal in (texto ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var palabra = palabraOriginal;
            while (true)
            {
                var prefijo = lineas.Count == 0 && actual.Length == 0 ? string.Empty : (actual.Length == 0 ? sangria : " ");
                if (actual.Length + prefijo.Length + palabra.Length <= ancho)
                {
                    actual.Append(prefijo).Append(palabra);
                    break;
                }
                var esLarga = palabra.Length > ancho - sangria.Length;
                if (actual.Length > 0 && !esLarga)
                {
                    lineas.Add(actual.ToString());
                    actual.Clear();
                    continue;
                }
                if (actual.Length > 0)
                {
                    // Palabra más larga que una línea: se parte aprovechando lo que queda de esta.
                    var resto = ancho - actual.Length - 1;
                    if (resto >= 3)
                    {
                        actual.Append(' ').Append(palabra[..resto]);
                        palabra = palabra[resto..];
                    }
                    lineas.Add(actual.ToString());
                    actual.Clear();
                    continue;
                }
                // Palabra más larga que la línea: se parte.
                var inicio = lineas.Count == 0 ? string.Empty : sangria;
                var cabe = ancho - inicio.Length;
                lineas.Add(inicio + palabra[..cabe]);
                palabra = palabra[cabe..];
                if (palabra.Length == 0) break;
            }
        }
        if (actual.Length > 0) lineas.Add(actual.ToString());
        if (lineas.Count == 0) lineas.Add(string.Empty);
        return lineas;
    }

    /// <summary>
    /// La comanda se envía en ASCII (ESC/POS básico): las tildes y la ñ se imprimirían como "?".
    /// </summary>
    private static string SinTildes(string texto) => NormalizarAscii(texto);

    private static string NormalizarAscii(string texto)
    {
        // Muchas impresoras térmicas ESC/POS económicas no traen configurada una tabla de
        // caracteres con tildes/ñ (CP858/Latin-1); para máxima compatibilidad se normalizan los
        // acentos a su letra base en ASCII plano.
        var normalizado = (texto ?? string.Empty).Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalizado.Length);
        foreach (var c in normalizado)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark)
                continue;
            sb.Append(c == '·' ? '-' : c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
