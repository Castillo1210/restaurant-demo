using CaraNegra.Domain.Entities;

namespace CaraNegra.Application.Auth.Interfaces;

public interface IJwtService
{
    string GenerarToken(Usuario usuario);
    DateTime ObtenerExpiracion();
}