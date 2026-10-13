using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SongVault.Application.Abstractions;
using SongVault.Infrastructure.Persistence;

namespace SongVault.Infrastructure.Jobs;

/// <summary>Prend la prochaine tâche due, l'exécute et enregistre le résultat (ADR 0011).</summary>
public sealed class BackgroundJobRunner(
    IServiceScopeFactory scopes, IOptions<BackgroundJobOptions> options, TimeProvider clock,
    ILogger<BackgroundJobRunner> logger)
{
    /// <summary>Exécute une tâche due. false : il n'y avait rien à faire.</summary>
    public async Task<bool> RunNextAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SongVaultDbContext>();
        var settings = options.Value;
        var now = clock.GetUtcNow();
        var stale = now - settings.StaleAfter;

        // Une seule instruction : deux instances ne prennent jamais la même ligne (UPDLOCK, READPAST)
        var claimed = await db.Database.SqlQuery<ClaimedJob>($"""
            WITH next AS (
                SELECT TOP (1) * FROM BackgroundJobs WITH (UPDLOCK, READPAST, ROWLOCK)
                WHERE (Status = 'Pending' AND RunAfter <= {now})
                   OR (Status = 'Running' AND StartedAt < {stale})
                ORDER BY RunAfter)
            UPDATE next SET Status = 'Running', Attempts = Attempts + 1, StartedAt = {now}
            OUTPUT inserted.Id, inserted.Type, inserted.Payload, inserted.Attempts, inserted.RunAfter
            """).ToListAsync(ct);
        if (claimed is not [var job]) return false;

        try
        {
            var handler = scope.ServiceProvider.GetServices<IBackgroundJobHandler>().SingleOrDefault(h => h.Type == job.Type)
                ?? throw new InvalidOperationException($"Aucun gestionnaire pour les tâches « {job.Type} ».");
            await handler.RunAsync(
                new BackgroundJobContext(job.Id, job.Payload, job.Attempts, job.Attempts >= settings.MaxAttempts), ct);

            await db.BackgroundJobs.Where(j => j.Id == job.Id).ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, BackgroundJobStatus.Succeeded)
                .SetProperty(j => j.CompletedAt, clock.GetUtcNow())
                .SetProperty(j => j.Error, (string?)null), ct);
        }
        // Arrêt de l'API : pas de catch, la tâche reste Running et sera reprise après StaleAfter
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            var failed = job.Attempts >= settings.MaxAttempts;
            var failedAt = clock.GetUtcNow();
            var runAfter = failed ? job.RunAfter : failedAt + TimeSpan.FromMinutes(Math.Pow(5, job.Attempts - 1));  // 1 min, puis 5 min
            DateTimeOffset? completedAt = failed ? failedAt : null;
            var status = failed ? BackgroundJobStatus.Failed : BackgroundJobStatus.Pending;
            var error = $"{ex.GetType().Name} : {ex.Message}";
            if (error.Length > BackgroundJob.ErrorMaxLength) error = error[..BackgroundJob.ErrorMaxLength];

            logger.LogError(ex, "Tâche {Type} {JobId} en échec (essai {Attempt}/{MaxAttempts}).",
                job.Type, job.Id, job.Attempts, settings.MaxAttempts);

            await db.BackgroundJobs.Where(j => j.Id == job.Id).ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, status)
                .SetProperty(j => j.RunAfter, runAfter)
                .SetProperty(j => j.CompletedAt, completedAt)
                .SetProperty(j => j.Error, error), CancellationToken.None);
        }

        return true;
    }

    /// <summary>Colonnes renvoyées par OUTPUT.</summary>
    private sealed class ClaimedJob
    {
        public Guid Id { get; set; }
        public string Type { get; set; } = "";
        public string Payload { get; set; } = "";
        public int Attempts { get; set; }
        public DateTimeOffset RunAfter { get; set; }
    }
}