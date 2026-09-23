namespace CaraNegra.Application.Auth.Interfaces;

public interface IPasswordService
{
    string HashPassword(string password);
    bool VerificarPassword(string password, string hash);
}