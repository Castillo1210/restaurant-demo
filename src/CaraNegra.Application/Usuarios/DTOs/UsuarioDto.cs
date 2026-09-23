using CaraNegra.Domain.Enums;

namespace CaraNegra.Application.Usuarios.DTOs;

public class UsuarioDto
{
    public int UsuarioId { get; set; }
    public string NombreUsuario { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public int RolId { get; set; }
    public string RolNombre { get; set; } = string.Empty;
    public bool EsVerificado { get; set; }
    public DateTime? UltimoAccesoEn { get; set; }
    public DateTime CreadoEn { get; set; }
}

public class CreateUsuarioDto
{
    public string NombreUsuario { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int RolId { get; set; }
}

public class UpdateUsuarioDto
{
    public string NombreCompleto { get; set; } = string.Empty;
    public int RolId { get; set; }
    public bool EsVerificado { get; set; }
}

public class ChangePasswordDto
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ResetPasswordDto
{
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}