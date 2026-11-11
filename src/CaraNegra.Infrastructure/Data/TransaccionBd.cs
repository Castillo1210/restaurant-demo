using CaraNegra.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace CaraNegra.Infrastructure.Data;

/// <summary>Envoltura de IDbContextTransaction. Liberarla sin confirmar = deshacer.</summary>
internal sealed class TransaccionBd : ITransaccionBd
{
    /// <summary>Sin transacción propia (base en memoria o transacción ya iniciada por quien llama).</summary>
    public static readonly ITransaccionBd Ninguna = new TransaccionBd(null);

    private readonly IDbContextTransaction? _transaccion;

    public TransaccionBd(IDbContextTransaction? transaccion) => _transaccion = transaccion;

    public Task ConfirmarAsync(CancellationToken cancellationToken = default) =>
        _transaccion?.CommitAsync(cancellationToken) ?? Task.CompletedTask;

    public ValueTask DisposeAsync() => _transaccion?.DisposeAsync() ?? ValueTask.CompletedTask;
}
