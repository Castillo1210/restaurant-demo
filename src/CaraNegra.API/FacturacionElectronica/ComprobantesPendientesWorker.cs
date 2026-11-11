using CaraNegra.Application.Comprobantes.Commands;
using MediatR;
using Microsoft.Extensions.Options;

namespace CaraNegra.API.FacturacionElectronica;

/// <summary>
/// Cada IntervaloReintentoSegundos reenvía los comprobantes "PorEnviar" (no llegaron al proveedor:
/// sin internet, caída, token) y consulta los "Pendiente" (SUNAT aún no responde).
/// Así un corte de internet en caja no deja boletas sin enviar. No hace nada en modo Simulado.
/// </summary>
public class ComprobantesPendientesWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly FacturacionElectronicaOptions _options;
    private readonly ILogger<ComprobantesPendientesWorker> _logger;

    public ComprobantesPendientesWorker(IServiceScopeFactory scopeFactory, IOptions<FacturacionElectronicaOptions> options,
        ILogger<ComprobantesPendientesWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var esSimulado = !string.Equals(_options.Proveedor, "ApiSunat", StringComparison.OrdinalIgnoreCase);
        if (esSimulado || _options.IntervaloReintentoSegundos <= 0)
            return;

        var intervalo = TimeSpan.FromSeconds(Math.Max(30, _options.IntervaloReintentoSegundos));

        // Pequeña espera inicial para no competir con el arranque (migraciones, seeders).
        try { await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken); }
        catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(intervalo);
        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var procesados = await mediator.Send(new ProcesarComprobantesPendientesCommand(), stoppingToken);
                if (procesados > 0)
                    _logger.LogInformation("Facturación: {Cantidad} comprobante(s) pendiente(s) reenviado(s)/consultado(s)", procesados);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando comprobantes pendientes");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}
