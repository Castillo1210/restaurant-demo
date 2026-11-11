using CaraNegra.Domain.Common;

namespace CaraNegra.Domain.Entities;

/// <summary>
/// Celular o tablet del restaurante habilitado por un ADMIN para el ingreso rápido con PIN.
///
/// Al registrarlo se genera un token aleatorio que queda guardado SOLO en ese navegador; aquí se
/// guarda únicamente su hash SHA-256. El login con PIN exige ese token, así que aunque alguien
/// tenga la dirección del sistema, desde un equipo no registrado no puede probar PINs: se le pide
/// usuario y contraseña.
/// </summary>
public class DispositivoAutorizado : BaseEntity
{
    public int DispositivoAutorizadoId { get; set; }

    /// <summary>Nombre para reconocerlo, p. ej. "Tablet terraza" o "Celular de Juan".</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>SHA-256 (hex) del token que guarda el dispositivo.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>
    /// Minutos sin uso tras los que se cierra la sesión de un MOZO en este dispositivo
    /// (para tablets compartidas). 0 = no se cierra sola.
    /// </summary>
    public int CierreInactividadMinutos { get; set; }

    public DateTime? UltimoUsoEn { get; set; }

    /// <summary>Usuario ADMIN que lo registró (solo informativo).</summary>
    public string? RegistradoPor { get; set; }
}
