using MySqlConnector;

namespace CaraNegra.API.DataSeeding;

/// <summary>
/// Espera a que MySQL acepte conexiones antes de sembrar datos y atender pedidos.
/// Como servicio de Windows, al encender la PC el sistema puede arrancar unos segundos antes que
/// MySQL; sin esta espera fallaría al iniciar.
///
/// La espera es corta (~20 s) a propósito: Windows da unos 30 s a un servicio para arrancar. Si
/// MySQL tarda más, el servicio termina con error y Windows lo vuelve a iniciar solo (la
/// instalación configura "reiniciar el servicio si falla" y la dependencia del servicio de MySQL).
/// </summary>
public static class EsperaBaseDeDatos
{
    public static async Task EsperarAsync(string connectionString, ILogger logger, int maxIntentos = 7)
    {
        for (var intento = 1; ; intento++)
        {
            try
            {
                await using var conexion = new MySqlConnection(connectionString);
                await conexion.OpenAsync();
                if (intento > 1)
                    logger.LogInformation("MySQL disponible tras {Intentos} intentos", intento);
                return;
            }
            catch (MySqlException ex) when (intento < maxIntentos)
            {
                if (intento == 1)
                    logger.LogWarning("MySQL aún no responde ({Mensaje}); reintentando…", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }
    }
}
