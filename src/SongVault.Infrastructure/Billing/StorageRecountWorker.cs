using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SongVault.Application.Billing;
using SongVault.Infrastructure.Jobs;

namespace SongVault.Infrastructure.Billing;

/// <summary>
/// Toutes les heures : s'assure que le recalcul de nuit du stockage est programmé (ADR 0023). Le recalcul
/// lui-même passe par les tâches de fond (ADR 0011), donc une seule instance le fait même avec deux API.
/// </summary>
internal sealed class StorageRecountWorker(
    IServiceScopeFactory scopes, IOptions<BackgroundJobOptions> jobs, TimeProvider clock,
    ILogger<StorageRecountWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!jobs.Value.Enabled) return;                 // tests d'intégration : rien en arrière-plan

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<StorageRecountScheduler>().EnsureScheduledAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Programmation du recalcul du stockage impossible.");
            }

            try
            {
                await Task.Delay(Interval, clock, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}