using System.Net;

namespace CaraNegra.API.Middleware;

/// <summary>
/// IP del cliente para el rate limiting.
///
/// En la demo el frontend se sirve con Vite, que reenvía /api y /hubs al backend (proxy), y se
/// publica por un túnel (cloudflared / Tailscale). Así, TODAS las peticiones llegan al backend desde
/// 127.0.0.1, y con la IP de conexión todos los equipos compartirían el mismo cupo por minuto.
///
/// Solo cuando la conexión viene de la propia PC (loopback) se confía en las cabeceras del proxy.
/// Una petición que llega directo desde otra máquina no puede falsear su IP con ellas.
///
/// Detrás del proxy se usa CF-Connecting-IP (la pone Cloudflare y el cliente no la controla) o,
/// si no está, el valor de X-Forwarded-For más a la DERECHA que no sea loopback: cada proxy agrega
/// la IP que vio al final, así que los valores de la izquierda los puede inventar el cliente (si se
/// tomara el primero, cambiándolo en cada intento se esquivaría el límite de intentos de login).
/// </summary>
public static class ClienteIp
{
    public static string Obtener(HttpContext context)
    {
        var remota = context.Connection.RemoteIpAddress;
        if (remota is not null && remota.IsIPv4MappedToIPv6)
            remota = remota.MapToIPv4();

        if (remota is not null && IPAddress.IsLoopback(remota))
        {
            var cloudflare = context.Request.Headers["CF-Connecting-IP"].ToString().Trim();
            if (IPAddress.TryParse(cloudflare, out var ipCf))
                return ipCf.ToString();

            var reenviada = context.Request.Headers["X-Forwarded-For"].ToString();
            if (!string.IsNullOrWhiteSpace(reenviada))
            {
                foreach (var valor in reenviada.Split(',').Select(v => v.Trim()).Reverse())
                {
                    if (IPAddress.TryParse(valor, out var ip) && !IPAddress.IsLoopback(ip))
                        return (ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip).ToString();
                }
            }
        }

        return remota?.ToString() ?? "unknown";
    }
}
