using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using SongVault.Infrastructure.Jobs;
using SongVault.Infrastructure.Persistence;

namespace SongVault.Api.ServiceStatus;

/// <summary>
/// #18 : une tâche de fond due depuis plus de 15 minutes (e-mail, analyse antivirus, aperçu…) rend le service
/// « dégradé » : tout s'utilise, mais ce qui passe par les tâches de fond attend.
/// </summary>
public sealed class BackgroundJobsHealthCheck(SongVaultDbContext db, TimeProvider clock) : IHealthCheck
{
    public static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(15);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var limit = clock.GetUtcNow() - MaxDelay;
        var late = await db.BackgroundJobs.AsNoTracking()
            .AnyAsync(j => j.Status == BackgroundJobStatus.Pending && j.RunAfter < limit, cancellationToken);
        return late ? HealthCheckResult.Degraded("Tâches de fond en retard.") : HealthCheckResult.Healthy();
    }
}