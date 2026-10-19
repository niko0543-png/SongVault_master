using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SongVault.Application.Trash;
using SongVault.Infrastructure.Jobs;

namespace SongVault.Infrastructure.Trash;

/// <summary>
/// Une fois par jour : vide la corbeille de ce qui y est depuis 30 jours (#20, ADR 0027). Deux API qui le font en
/// même temps effacent au pire deux fois la même chose, sans dommage (une ligne en double au journal d'audit).
/// </summary>
internal sealed class TrashPurgeWorker(
    IServiceScopeFactory scopes, IOptions<BackgroundJobOptions> jobs, TimeProvider clock,
    ILogger<TrashPurgeWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!jobs.Value.Enabled) return;                 // tests d'intégration : rien en arrière-plan

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var purged = await scope.ServiceProvider.GetRequiredService<PurgeExpiredTrashHandler>().HandleAsync(stoppingToken);
                if (purged > 0) logger.LogInformation("Corbeille : {Count} élément(s) effacé(s) après 30 jours.", purged);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Purge de la corbeille impossible ; nouvel essai demain.");
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