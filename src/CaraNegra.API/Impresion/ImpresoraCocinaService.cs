using System.Linq;
using System.Net.Sockets;
using System.Text;
using CaraNegra.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace CaraNegra.API.Impresion;

/// <summary>
/// Envía comandas a una impresora térmica ESC/POS conectada por red (típicamente puerto
/// 9100/JetDirect), abriendo una conexión TCP simple y mandando el ticket como texto plano.
/// Ver CONFIGURACION_LOCAL.md, sección "Fase 6 — Ticketera", para cómo configurar la IP real.
/// </summary>
public class ImpresoraCocinaService : IImpresoraCocinaService
{
    private readonly ImpresoraCocinaOptions _options;
    private readonly ILogger<ImpresoraCocinaService> _logger;

    private const string LineaSeparadora = "--------------------------------";

    public ImpresoraCocinaService(IOptions<ImpresoraCocinaOptions> options, ILogger<ImpresoraCocinaService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task ImprimirComandaAsync(ComandaCocina comanda, CancellationToken cancellationToken = default)
    {
        if (!_options.Habilitada || string.IsNullOrWhiteSpace(_options.Ip))
        {
            _logger.LogInformation(
                "Impresora de cocina deshabilitada o sin configurar; se omite la impresión de la comanda del pedido {PedidoId}.",
                comanda.PedidoId);
            return;
        }

        try
        {
            var ticket = ConstruirTicket(comanda);

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(_options.TimeoutMs));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            using var cliente = new TcpClient();
            await cliente.ConnectAsync(_options.Ip, _options.Puerto, linkedCts.Token);

            await using var stream = cliente.GetStream();
            await stream.WriteAsync(ticket, linkedCts.Token);
            await stream.FlushAsync(linkedCts.Token);
        }
        catch (Exception ex)
        {
            // Nunca se relanza: la impresora apagada/desconectada/mal configurada no debe
            // impedir que el mozo tome pedidos ni que caja cobre.
            _logger.LogWarning(ex,
                "No se pudo imprimir la comanda del pedido {PedidoId} en la impresora de cocina ({Ip}:{Puerto}).",
                comanda.PedidoId, _options.Ip, _options.Puerto);
        }
    }

    public string PrevisualizarComanda(ComandaCocina comanda)
    {
        var ticketBytes = ConstruirTicket(comanda);
        // El primer comando (ESC @) es un byte de control para la impresora, no texto
        // imprimible — se quita para que la previsualización en pantalla se vea limpia.
        var texto = Encoding.ASCII.GetString(ticketBytes);
        return texto.Replace("@", string.Empty).Trim('\n');
    }

    private static byte[] ConstruirTicket(ComandaCocina comanda)
    {
        var sb = new StringBuilder();

        // ESC @ (0x1B 0x40): comando ESC/POS estándar para inicializar/resetear la impresora.
        sb.Append('\u001B').Append('@');

        AgregarLinea(sb, comanda.EsAdicional ? "*** PEDIDO ADICIONAL ***" : "CARA NEGRA - COCINA");
        AgregarLinea(sb, LineaSeparadora);
        AgregarLinea(sb, $"Mesa: {comanda.MesaNumero}          Pedido #{comanda.PedidoId}");
        AgregarLinea(sb, $"Mozo: {comanda.MozoNombre}");
        AgregarLinea(sb, $"Hora: {comanda.CreadoEn:dd/MM/yyyy HH:mm}");
        AgregarLinea(sb, LineaSeparadora);

        foreach (var item in comanda.Items)
        {
            AgregarLinea(sb, $"{item.Cantidad}x {item.ProductoNombre}");
            if (!string.IsNullOrWhiteSpace(item.Notas))
            {
                AgregarLinea(sb, $"   Nota: {item.Notas}");
            }
        }

        AgregarLinea(sb, LineaSeparadora);
        // Alimenta papel suficiente para poder cortar a mano el ticket.
        sb.Append('\n').Append('\n').Append('\n').Append('\n');

        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    // Se usa '\n' explícito (no StringBuilder.AppendLine, cuyo separador depende del SO
    // donde corra el backend) para que el formato del ticket sea idéntico sin importar
    // si el proceso corre en Windows o Linux.
    private static void AgregarLinea(StringBuilder sb, string texto) => sb.Append(NormalizarAscii(texto)).Append('\n');

    private static string NormalizarAscii(string texto)
    {
        // Muchas impresoras térmicas ESC/POS económicas no traen configurada una tabla de
        // caracteres con tildes/ñ (CP858/Latin-1); para máxima compatibilidad por defecto
        // se normalizan los acentos a su letra base en ASCII plano. Si tu impresora sí
        // soporta una codepage con español, cambia Encoding.ASCII por Encoding.Latin1 en
        // ConstruirTicket y esta normalización deja de ser necesaria.
        var normalizado = texto.Normalize(NormalizationForm.FormD);
        var caracteres = normalizado
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray();
        return new string(caracteres);
    }
}
