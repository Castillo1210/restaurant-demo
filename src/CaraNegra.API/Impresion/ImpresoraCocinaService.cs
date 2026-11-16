using System.Net.Sockets;
using System.Text;
using CaraNegra.API.Hubs;
using CaraNegra.Application.Common.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace CaraNegra.API.Impresion;

/// <summary>
/// Envía comandas a una impresora térmica ESC/POS conectada por red (típicamente puerto
/// 9100/JetDirect), abriendo una conexión TCP simple y mandando el ticket.
///
/// Formato compacto para no gastar papel: la mesa en letra grande (es lo que cocina busca), el
/// tipo de ticket (ADICIONAL, ANULADO…) en doble alto, mozo y hora en una sola línea, y los
/// productos seguidos, sin líneas en blanco entre ellos, en el tamaño configurado
/// (ImpresoraCocina:TamanoProductos). Corte de papel al final (sin cortador se ignora).
///
/// El ticket se dibuja como IMAGEN con una letra TrueType (Carlito, la misma familia que Calibri)
/// y se manda en modo raster (GS v 0): sale más fina y proporcional que la letra interna de la
/// ticketera, que a doble tamaño se ve gruesa. Si no se puede dibujar, se imprime en modo texto.
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
            var ticket = ConstruirTicketParaImprimir(comanda);

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
        foreach (var linea in ArmarComanda(comanda))
        {
            var ancho = AnchoDe(linea.Tam);
            var texto = NormalizarAscii(linea.Texto);
            sb.Append(linea.Centrada ? texto.PadLeft((ancho + texto.Length) / 2) : texto).Append('\n');
            if (linea.EspacioDespues) sb.Append('\n');
        }
        return sb.ToString().TrimEnd('\n');
    }

    // ───────────── Armado de la comanda ─────────────

    /// <summary>
    /// Tamaños de letra de la ticketera (fuente A = la normal, 12 puntos de ancho; fuente B = la
    /// compacta, 9 puntos):
    ///   Normal     fuente A 1x1 → 42 caracteres por línea (80 mm)
    ///   Alto       fuente A 1x2 (doble alto) → 42
    ///   Intermedio fuente B 2x2 (doble alto y ancho) → 28: entre Alto y Grande
    ///   Grande     fuente A 2x2 (doble alto y ancho) → 21
    /// </summary>
    private enum Tamano { Normal, Alto, Intermedio, Grande }

    /// <summary>
    /// Una línea del ticket ya cortada al ancho que le corresponde según su tamaño.
    /// InicioNegrita/LargoNegrita: un tramo de la línea en negrita (p. ej. solo el nombre del mozo).
    /// LargoDerecha: los últimos caracteres van alineados a la derecha (la hora), en el modo imagen.
    /// Producto: línea de un plato (o su nota); en el modo imagen usa la escala de TamanoProductos.
    /// </summary>
    private sealed record Linea(string Texto, Tamano Tam = Tamano.Normal, bool Negrita = false,
        bool Centrada = false, bool EspacioDespues = false, int InicioNegrita = 0, int LargoNegrita = 0,
        int LargoDerecha = 0, bool Producto = false);

    /// <summary>Caracteres por línea en letra normal (80 mm: 42-48; 58 mm: 32).</summary>
    private int Columnas => Math.Clamp(_options.Columnas, 24, 64);

    private Tamano TamanoProductos => (_options.TamanoProductos ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "grande" => Tamano.Grande,
        "intermedia" or "intermedio" => Tamano.Intermedio,
        "normal" => Tamano.Normal,
        _ => Tamano.Alto
    };

    /// <summary>Caracteres que entran en una línea con ese tamaño de letra (ver <see cref="Tamano"/>).</summary>
    private int AnchoDe(Tamano tamano) => tamano switch
    {
        Tamano.Grande => Columnas / 2,
        Tamano.Intermedio => Columnas * 2 / 3,
        _ => Columnas
    };

    /// <param name="platosEnUnaLinea">
    /// Modo imagen: cada plato (y su nota) en UNA sola línea; si no entra, se achica la letra de
    /// esa línea en vez de pasar a la siguiente.
    /// </param>
    private List<Linea> ArmarComanda(ComandaCocina comanda, bool platosEnUnaLinea = false)
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
        foreach (var parte in Envolver(etiquetaMesa, columnas, string.Empty))
            lineas.Add(new Linea(parte, Tamano.Alto, Negrita: true, Centrada: true));
        if (palabrasSalon.Length > 1)
            lineas.Add(new Linea($"({salon})", Centrada: true));

        // Mozo (o quien anuló) y la hora en UNA línea; sin número de pedido (cocina se guía por la mesa).
        // CreadoEn se guarda en UTC: se imprime en hora de Lima.
        var hora = CaraNegra.Application.Common.PeruDateRangeHelper.ToHoraPeru(comanda.CreadoEn);
        var textoHora = $"{hora:dd/MM HH:mm}";
        var rotuloPersona = comanda.EsAnulacion ? "Anulo" : comanda.EsCambioComentario ? "Por" : "Mozo";
        var persona = $"{rotuloPersona}: {comanda.MozoNombre}";
        // Solo el nombre va en negrita (no "Mozo:" ni la hora).
        var inicioNombre = rotuloPersona.Length + 2;
        if (persona.Length + 1 + textoHora.Length <= columnas)
            lineas.Add(new Linea(Columnas2(persona, textoHora, columnas), Tamano.Alto,
                InicioNegrita: inicioNombre, LargoNegrita: persona.Length - inicioNombre, LargoDerecha: textoHora.Length));
        else
        {
            foreach (var parte in Envolver(persona, columnas, new string(' ', inicioNombre)))
                lineas.Add(new Linea(parte, Tamano.Alto,
                    InicioNegrita: inicioNombre, LargoNegrita: Math.Max(0, parte.Length - inicioNombre)));
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
        var ancho = AnchoDe(tam);
        // Los platos van siempre sin negrita, en cualquier tamaño.
        const bool negrita = false;
        foreach (var item in comanda.Items)
        {
            var prefijo = $"{item.Cantidad} x ";
            var textoPlato = prefijo + item.ProductoNombre.Trim();
            var partesPlato = platosEnUnaLinea
                ? new List<string> { textoPlato }
                : Envolver(textoPlato, ancho, new string(' ', prefijo.Length));
            foreach (var parte in partesPlato)
                lineas.Add(new Linea(parte, tam, Negrita: negrita, Producto: true));

            // La nota del plato ("sin cebolla") justo debajo, del mismo tamaño y marcada con ">>".
            if (!string.IsNullOrWhiteSpace(item.Notas))
            {
                var textoNota = $">> {item.Notas!.Trim()}";
                var partesNota = platosEnUnaLinea ? new List<string> { textoNota } : Envolver(textoNota, ancho, "   ");
                foreach (var parte in partesNota)
                    lineas.Add(new Linea(parte, tam, Negrita: negrita, Producto: true));
            }
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
            b.AddRange(new byte[] { 0x1B, 0x4D, (byte)(linea.Tam == Tamano.Intermedio ? 1 : 0) }); // ESC M: fuente A / B
            b.AddRange(new byte[] { 0x1D, 0x21, linea.Tam switch                       // GS !: tamaño
            {
                Tamano.Grande or Tamano.Intermedio => (byte)0x11,   // doble ancho + doble alto
                Tamano.Alto => (byte)0x01,     // doble alto
                _ => (byte)0x00
            } });
            if (linea.LargoNegrita > 0 && linea.InicioNegrita + linea.LargoNegrita <= linea.Texto.Length)
            {
                // Tramo en negrita dentro de la línea: ESC E 1 antes y ESC E 0 después.
                var fin = linea.InicioNegrita + linea.LargoNegrita;
                b.AddRange(Encoding.ASCII.GetBytes(NormalizarAscii(linea.Texto[..linea.InicioNegrita])));
                b.AddRange(new byte[] { 0x1B, 0x45, 0x01 });
                b.AddRange(Encoding.ASCII.GetBytes(NormalizarAscii(linea.Texto[linea.InicioNegrita..fin])));
                b.AddRange(new byte[] { 0x1B, 0x45, (byte)(linea.Negrita ? 1 : 0) });
                b.AddRange(Encoding.ASCII.GetBytes(NormalizarAscii(linea.Texto[fin..])));
            }
            else
                b.AddRange(Encoding.ASCII.GetBytes(NormalizarAscii(linea.Texto)));
            b.Add(0x0A);
            if (linea.EspacioDespues)
            {
                b.AddRange(new byte[] { 0x1D, 0x21, 0x00 });  // el espacio entre productos en tamaño normal
                b.Add(0x0A);
            }
        }
        b.AddRange(new byte[] { 0x1D, 0x21, 0x00, 0x1B, 0x45, 0x00, 0x1B, 0x61, 0x00, 0x1B, 0x4D, 0x00 }); // volver a normal
        b.AddRange(new byte[] { 0x1B, 0x64, 0x04 });          // ESC d 4: avanzar 4 líneas
        b.AddRange(new byte[] { 0x1D, 0x56, 0x42, 0x00 });    // GS V B 0: cortar (si tiene cortador)
        return b.ToArray();
    }

    // ───────────── Ticket como imagen (letra TrueType) ─────────────

    /// <summary>Primero como imagen; si falla (librería gráfica o letra), en modo texto: la comanda siempre sale.</summary>
    private byte[] ConstruirTicketParaImprimir(ComandaCocina comanda)
    {
        try
        {
            return ConstruirTicketImagen(comanda);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo dibujar la comanda del pedido {PedidoId} como imagen; se imprime en modo texto.",
                comanda.PedidoId);
            return ConstruirTicket(comanda);
        }
    }

    /// <summary>Ancho imprimible en puntos (203 ppp): 80 mm → 576; 58 mm → 384.</summary>
    private int AnchoPuntos => Columnas >= 42 ? 576 : 384;

    private const int MargenPuntos = 4;

    /// <summary>
    /// Alto de la letra en puntos (modo imagen). Los platos siguen ImpresoraCocina:TamanoProductos
    /// en orden Grande &gt; Intermedia &gt; Mediana (Alto) &gt; Pequeña (Normal); el encabezado
    /// (mesa, mozo, avisos) tiene su propio tamaño fijo.
    /// </summary>
    private static float PuntosDe(Linea linea) => linea.Producto
        ? linea.Tam switch
        {
            Tamano.Grande => 33f,
            Tamano.Intermedio => 30f,
            Tamano.Alto => 26f,
            _ => 22f
        }
        : linea.Tam switch
        {
            Tamano.Grande => 38f,
            Tamano.Alto or Tamano.Intermedio => 32f,
            _ => 24f
        };

    private static readonly Lazy<SKTypeface> FuenteNormal = new(() => CargarFuente("Fuentes.Carlito-Regular.ttf"));
    private static readonly Lazy<SKTypeface> FuenteNegrita = new(() => CargarFuente("Fuentes.Carlito-Bold.ttf"));

    private static SKTypeface CargarFuente(string recurso)
    {
        using var stream = typeof(ImpresoraCocinaService).Assembly.GetManifestResourceStream(recurso)
            ?? throw new InvalidOperationException($"No está la letra {recurso} dentro del programa.");
        using var copia = new MemoryStream();
        stream.CopyTo(copia);
        return SKTypeface.FromData(SKData.CreateCopy(copia.ToArray()))
            ?? throw new InvalidOperationException($"No se pudo leer la letra {recurso}.");
    }

    private static bool EsSeparador(Linea linea) => linea.Texto.Length > 0 && linea.Texto.All(c => c == '=');

    /// <summary>La línea en tramos (normal / negrita) y, aparte, el texto alineado a la derecha.</summary>
    private static (List<(string Texto, bool Negrita)> Tramos, string? Derecha) Partir(Linea linea)
    {
        var texto = linea.Texto;
        string? derecha = null;
        if (linea.LargoDerecha > 0 && linea.LargoDerecha < texto.Length)
        {
            derecha = texto[^linea.LargoDerecha..];
            texto = texto[..^linea.LargoDerecha].TrimEnd();
        }

        var tramos = new List<(string Texto, bool Negrita)>();
        if (linea.LargoNegrita > 0 && linea.InicioNegrita + linea.LargoNegrita <= texto.Length)
        {
            var fin = linea.InicioNegrita + linea.LargoNegrita;
            tramos.Add((texto[..linea.InicioNegrita], linea.Negrita));
            tramos.Add((texto[linea.InicioNegrita..fin], true));
            tramos.Add((texto[fin..], linea.Negrita));
        }
        else
            tramos.Add((texto, linea.Negrita));
        return (tramos, derecha);
    }

    private static float AnchoTramos(List<(string Texto, bool Negrita)> tramos, SKFont normal, SKFont negrita) =>
        tramos.Sum(t => (t.Negrita ? negrita : normal).MeasureText(t.Texto));

    private byte[] ConstruirTicketImagen(ComandaCocina comanda)
    {
        var ancho = AnchoPuntos;
        var util = ancho - 2 * MargenPuntos;

        // 1) Medir: cada línea con su tamaño de letra, reducido si no entra a lo ancho.
        var plan = new List<(Linea Linea, float Letra, float Alto)>();
        float altoTotal = 0;
        foreach (var linea in ArmarComanda(comanda, platosEnUnaLinea: true))
        {
            float letra = 0, alto;
            if (EsSeparador(linea))
                alto = 14;
            else
            {
                letra = PuntosDe(linea);
                using var normal = new SKFont(FuenteNormal.Value, letra);
                using var negrita = new SKFont(FuenteNegrita.Value, letra);
                var (tramos, derecha) = Partir(linea);
                var anchoTexto = AnchoTramos(tramos, normal, negrita);
                if (derecha is not null)
                    anchoTexto += (linea.Negrita ? negrita : normal).MeasureText("  " + derecha);
                if (anchoTexto > util)
                    letra *= util / anchoTexto;
                alto = letra * 1.2f;
            }
            if (linea.EspacioDespues) alto += 10;
            plan.Add((linea, letra, alto));
            altoTotal += alto;
        }
        var altoPuntos = (int)Math.Ceiling(altoTotal) + 4;

        // 2) Dibujar.
        using var bitmap = new SKBitmap(new SKImageInfo(ancho, altoPuntos, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        using (var tinta = new SKPaint { Color = SKColors.Black, IsAntialias = true })
        {
            canvas.Clear(SKColors.White);
            float y = 0;
            foreach (var (linea, letra, alto) in plan)
            {
                if (EsSeparador(linea))
                    canvas.DrawRect(MargenPuntos, y + 6, util, 2, tinta);
                else
                {
                    using var normal = new SKFont(FuenteNormal.Value, letra) { Edging = SKFontEdging.Antialias };
                    using var negrita = new SKFont(FuenteNegrita.Value, letra) { Edging = SKFontEdging.Antialias };
                    var (tramos, derecha) = Partir(linea);
                    var baseY = y + letra * 0.9f;
                    var x = linea.Centrada
                        ? MargenPuntos + Math.Max(0, (util - AnchoTramos(tramos, normal, negrita)) / 2)
                        : MargenPuntos;
                    foreach (var (texto, esNegrita) in tramos)
                    {
                        if (texto.Length == 0) continue;
                        var fuente = esNegrita ? negrita : normal;
                        canvas.DrawText(texto, x, baseY, fuente, tinta);
                        x += fuente.MeasureText(texto);
                    }
                    if (derecha is not null)
                    {
                        var fuente = linea.Negrita ? negrita : normal;
                        canvas.DrawText(derecha, MargenPuntos + util - fuente.MeasureText(derecha), baseY, fuente, tinta);
                    }
                }
                y += alto;
            }
        }

        // 3) A blanco y negro, 1 bit por punto (el bit más alto es el punto de la izquierda).
        var bytesPorFila = ancho / 8;
        var datos = new byte[bytesPorFila * altoPuntos];
        var pixeles = bitmap.GetPixelSpan();
        var bytesFilaBitmap = bitmap.RowBytes;
        for (var fila = 0; fila < altoPuntos; fila++)
            for (var col = 0; col < ancho; col++)
                if (pixeles[fila * bytesFilaBitmap + col * 4] < 128)   // canal rojo: el texto es gris
                    datos[fila * bytesPorFila + (col >> 3)] |= (byte)(0x80 >> (col & 7));

        // 4) ESC/POS: GS v 0 por bloques (algunas ticketeras tienen poco búfer), avance y corte.
        var b = new List<byte>(datos.Length + 256) { 0x1B, 0x40 };   // ESC @: inicializar
        const int filasPorBloque = 128;
        for (var inicio = 0; inicio < altoPuntos; inicio += filasPorBloque)
        {
            var filas = Math.Min(filasPorBloque, altoPuntos - inicio);
            b.AddRange(new byte[]
            {
                0x1D, 0x76, 0x30, 0x00,
                (byte)(bytesPorFila & 0xFF), (byte)(bytesPorFila >> 8),
                (byte)(filas & 0xFF), (byte)(filas >> 8)
            });
            b.AddRange(new ArraySegment<byte>(datos, inicio * bytesPorFila, filas * bytesPorFila));
        }
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
