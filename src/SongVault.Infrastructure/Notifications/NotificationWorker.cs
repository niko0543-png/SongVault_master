using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SongVault.Application.Notifications;

namespace SongVault.Infrastructure.Notifications;

/// <summary>
/// Toutes les DispatchInterval : prépare les notifications des nouveaux événements, lot par lot,
/// et s'assure que le prochain résumé quotidien est programmé (ADR 0018). Les e-mails partent ensuite
/// par les tâches de fond (ADR 0011), jamais pendant une requête HTTP.
/// </summary>
internal sealed class NotificationWorker(
    IServiceScopeFactory scopes, IOptions<NotificationOptions> options, TimeProvider clock,
    ILogger<NotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<NotificationDispatcher>();
                await dispatcher.EnsureDigestScheduledAsync(stoppingToken);
                while (await dispatcher.RunOnceAsync(stoppingToken) > 0) { }       // tout ce qui s'est accumulé
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // Base injoignable, par exemple : on réessaie au prochain tour plutôt que d'arrêter l'API
                logger.LogError(ex, "Préparation des notifications impossible.");
            }

            try
            {
                await Task.Delay(options.Value.DispatchInterval, clock, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}