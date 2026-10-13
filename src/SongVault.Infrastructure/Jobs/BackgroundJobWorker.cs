using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SongVault.Infrastructure.Jobs;

/// <summary>Lance les tâches dues les unes après les autres ; attend PollInterval quand il n'y en a plus.</summary>
internal sealed class BackgroundJobWorker(
    BackgroundJobRunner runner, IOptions<BackgroundJobOptions> options, TimeProvider clock,
    ILogger<BackgroundJobWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await runner.RunNextAsync(stoppingToken)) continue;     // il y en a peut-être d'autres
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // Base injoignable, par exemple : on réessaie au prochain tour plutôt que d'arrêter l'API
                logger.LogError(ex, "Lecture des tâches de fond impossible.");
            }

            try
            {
                await Task.Delay(options.Value.PollInterval, clock, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}