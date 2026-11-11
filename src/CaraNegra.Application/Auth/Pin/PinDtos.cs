using CaraNegra.Application.Auth.DTOs;

namespace CaraNegra.Application.Auth.Pin;

public class LoginPinRequest
{
    public int UsuarioId { get; set; }
    public string Pin { get; set; } = string.Empty;
}

public class LoginPinResponse : LoginResponse
{
    public int UsuarioId { get; set; }

    /// <summary>Del dispositivo: minutos sin uso para cerrar la sesión del MOZO (0 = nunca).</summary>
    public int CierreInactividadMinutos { get; set; }
}

/// <summary>Lo que muestra la pantalla de ingreso con PIN de un dispositivo registrado.</summary>
public class PantallaPinDto
{
    public int DispositivoId { get; set; }
    public string Dispositivo { get; set; } = string.Empty;
    public int CierreInactividadMinutos { get; set; }
    public List<UsuarioPinDto> Usuarios { get; set; } = new();
}

public class UsuarioPinDto
{
    public int UsuarioId { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
}

public class AsignarPinDto
{
    public string Pin { get; set; } = string.Empty;
}

public class DispositivoDto
{
    public int DispositivoId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int CierreInactividadMinutos { get; set; }
    public DateTime CreadoEn { get; set; }
    public DateTime? UltimoUsoEn { get; set; }
    public string? RegistradoPor { get; set; }
}

public class RegistrarDispositivoDto
{
    public string Nombre { get; set; } = string.Empty;
    public int CierreInactividadMinutos { get; set; }
}

/// <summary>Respuesta del registro: el token se entrega UNA sola vez, para guardarlo en el navegador.</summary>
public class DispositivoRegistradoDto
{
    public DispositivoDto Dispositivo { get; set; } = new();
    public string Token { get; set; } = string.Empty;
}
