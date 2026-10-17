using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SongVault.Application.Files;
using SongVault.Infrastructure.Jobs;

namespace SongVault.Infrastructure.Antivirus;

/// <summary>
/// Au démarrage, antivirus activé : remet en analyse les fichiers envoyés pendant qu'il était désactivé (ADR 0024).
/// Sans effet ensuite : il n'y a plus de fichier NotScanned tant que le réglage reste sur ClamAv.
/// </summary>
internal sealed class AntivirusActivationWorker(
    IServiceScopeFactory scopes, IOptions<AntivirusOptions> antivirus, IOptions<BackgroundJobOptions> jobs,
    ILogger<AntivirusActivationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!jobs.Value.Enabled || antivirus.Value.Provider != AntivirusOptions.ClamAvProvider) return;

        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var count = await scope.ServiceProvider.GetRequiredService<RescanNotScannedFilesHandler>().HandleAsync(stoppingToken);
            if (count > 0) logger.LogInformation("Antivirus activé : {Count} fichiers non analysés remis en analyse.", count);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            // Pas bloquant : les fichiers restent lisibles (NotScanned) et la reprise recommence au prochain démarrage
            logger.LogError(ex, "Reprise des fichiers non analysés impossible.");
        }
    }
}