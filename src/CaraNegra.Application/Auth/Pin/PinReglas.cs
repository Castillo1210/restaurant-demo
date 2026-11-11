using System.Security.Cryptography;
using System.Text;

namespace CaraNegra.Application.Auth.Pin;

/// <summary>
/// Reglas del ingreso rápido con PIN de 4 dígitos.
///
/// Un PIN de 4 dígitos solo tiene 10 000 combinaciones, así que NO reemplaza a la contraseña por
/// sí solo. Se protege con tres cosas:
///   1. Solo funciona desde dispositivos registrados por un ADMIN (token de dispositivo).
///   2. Solo para MOZO y COCINA (CAJERO y ADMIN manejan dinero y configuración: contraseña).
///   3. Bloqueo temporal del PIN tras varios intentos fallidos seguidos.
/// </summary>
public static class PinReglas
{
    public static readonly string[] RolesConPin = ["MOZO", "COCINA"];

    public const int MaxIntentosFallidos = 5;
    public const int MinutosBloqueo = 10;

    public static bool RolPermitePin(string? rol) =>
        rol is not null && RolesConPin.Any(r => r.Equals(rol, StringComparison.OrdinalIgnoreCase));

    public static bool TieneFormatoValido(string? pin) =>
        pin is { Length: 4 } && pin.All(char.IsAsciiDigit);

    /// <summary>
    /// PINs fáciles de adivinar: 0000, 1111… (todos iguales), 1234, 2345… 7890 (seguidos hacia
    /// arriba) y 9876, 4321… (seguidos hacia abajo).
    /// </summary>
    public static bool EsTrivial(string pin)
    {
        if (!TieneFormatoValido(pin)) return false;
        if (pin.Distinct().Count() == 1) return true;

        const string ascendente = "01234567890";
        const string descendente = "09876543210";
        return ascendente.Contains(pin) || descendente.Contains(pin);
    }
}

/// <summary>Token de dispositivo: se genera aleatorio y solo se guarda su hash.</summary>
public static class TokenDispositivo
{
    public const string Header = "X-Dispositivo-Token";

    public static string Generar()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim()))).ToLowerInvariant();
}

/// <summary>El dispositivo no está registrado (o fue revocado): hay que usar usuario y contraseña.</summary>
public class DispositivoNoAutorizadoException : Exception
{
    public DispositivoNoAutorizadoException()
        : base("Este dispositivo no está habilitado para ingresar con PIN. Ingresa con usuario y contraseña.") { }
}
