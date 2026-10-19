using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SongVault.Domain.Accounts;
using SongVault.Domain.Audit;
using SongVault.Infrastructure.Jobs;
using SongVault.Infrastructure.Persistence;

namespace SongVault.Infrastructure.Audit;

/// <summary>
/// Une fois par jour : efface le journal d'audit de plus d'un an et l'historique de connexion de plus de 90 jours
/// (ADR 0026). Deux API qui le font en même temps effacent les mêmes lignes : sans effet de bord.
/// </summary>
internal sealed class RetentionWorker(
    IServiceScopeFactory scopes, IOptions<BackgroundJobOptions> jobs, TimeProvider clock,
    ILogger<RetentionWorker> logger) : BackgroundService
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
                await PurgeAsync(scope.ServiceProvider.GetRequiredService<SongVaultDbContext>(), clock.GetUtcNow(), stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Rétention du journal d'audit et de l'historique de connexion impossible.");
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

    /// <summary>Le déclencheur de AuditEntries laisse passer ces suppressions-là, et seulement elles.</summary>
    public static async Task PurgeAsync(SongVaultDbContext db, DateTimeOffset now, CancellationToken ct)
    {
        var audit = now - AuditEntry.Retention;
        await db.AuditEntries.Where(e => e.OccurredAt < audit).ExecuteDeleteAsync(ct);
        var events = now - AccountEvent.Retention;
        await db.AccountEvents.Where(e => e.OccurredAt < events).ExecuteDeleteAsync(ct);
    }
}