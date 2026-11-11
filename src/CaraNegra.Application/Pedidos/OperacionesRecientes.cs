using System.Collections.Concurrent;

namespace CaraNegra.Application.Pedidos;

/// <summary>
/// Recuerda por unos minutos las operaciones ya procesadas (código único que genera la tablet por
/// cada carrito, o la caja por cada cobro). Si el Wi-Fi corta la RESPUESTA y se vuelve a tocar
/// "Enviar" o "Registrar cobro", la segunda petición llega con el mismo código y se devuelve lo
/// que ya quedó (pedido o pago), sin agregar los platos otra vez ni cobrar dos veces.
///
/// Vive en memoria (singleton): si el servicio se reinicia justo entre el envío y el reintento,
/// se pierde el recuerdo; es un caso rarísimo y no vale una tabla en la base para cubrirlo.
/// </summary>
public sealed class OperacionesRecientes
{
    private static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(15);
    public const int LargoMaximo = 64;

    // Id del resultado: el pedido (envíos del mozo) o el pago (cobros de caja).
    private readonly ConcurrentDictionary<string, (int Id, DateTime Hasta)> _hechas = new();

    /// <summary>
    /// Clave interna, o null si el envío no trae código (versiones viejas de la app). Es la misma
    /// para "crear pedido" y "agregar platos": si el pedido se creó pero la respuesta se perdió, el
    /// reintento puede llegar como "agregar" (la mesa ya figura ocupada) y debe reconocerse igual.
    /// </summary>
    public static string? Clave(string? operacionId)
    {
        var id = operacionId?.Trim();
        if (string.IsNullOrEmpty(id)) return null;
        return $"op:{(id.Length > LargoMaximo ? id[..LargoMaximo] : id)}";
    }

    /// <summary>Mensaje cuando el mismo envío llega para OTRO pedido o mesa (no se duplica en ningún lado).</summary>
    public static string MensajeYaRegistrado(int pedidoId, string mesa) =>
        $"Estos platos ya se registraron en el pedido #{pedidoId} ({mesa}). Revisa esa mesa antes de volver a enviarlos.";

    public bool YaProcesada(string clave, out int id)
    {
        if (_hechas.TryGetValue(clave, out var hecha) && hecha.Hasta > DateTime.UtcNow)
        {
            id = hecha.Id;
            return true;
        }
        id = 0;
        return false;
    }

    /// <summary>Se llama ANTES de confirmar la transacción (ver Olvidar si la confirmación falla).</summary>
    public void Registrar(string clave, int id)
    {
        var ahora = DateTime.UtcNow;
        _hechas[clave] = (id, ahora + Vigencia);

        // Limpieza de vencidos (son pocos: unos cientos de envíos por día).
        foreach (var (k, v) in _hechas)
            if (v.Hasta <= ahora) _hechas.TryRemove(k, out _);
    }

    public void Olvidar(string clave) => _hechas.TryRemove(clave, out _);
}
