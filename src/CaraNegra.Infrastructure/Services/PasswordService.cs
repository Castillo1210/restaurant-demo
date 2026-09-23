using CaraNegra.Application.Auth.Interfaces;

namespace CaraNegra.Infrastructure.Services;

public class PasswordService : IPasswordService
{
    public string HashPassword(string password) => BCrypt.Net.BCrypt.HashPassword(password);

    public bool VerificarPassword(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
}