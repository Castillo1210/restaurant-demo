namespace CaraNegra.Application.Common.Interfaces;

/// <summary>
/// Transacción de base de datos (ver IApplicationDbContext.IniciarTransaccionAsync).
/// Uso: <c>await using var tx = await _context.IniciarTransaccionAsync(ct);</c> ... 
/// <c>await tx.ConfirmarAsync(ct);</c>. Sin ConfirmarAsync, al salir se deshace.
/// </summary>
public interface ITransaccionBd : IAsyncDisposable
{
    Task ConfirmarAsync(CancellationToken cancellationToken = default);
}
