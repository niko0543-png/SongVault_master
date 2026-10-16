using SongVault.Application.Abstractions;

namespace SongVault.Application.Billing;

/// <summary>Programme le prochain recalcul de nuit s'il ne l'est pas déjà. Appelé par StorageRecountWorker.</summary>
public sealed class StorageRecountScheduler(IBackgroundJobs jobs, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public async Task EnsureScheduledAsync(CancellationToken ct)
    {
        if (await jobs.HasPendingAsync(StorageRecountJob.JobType, ct)) return;
        jobs.Enqueue(StorageRecountJob.JobType, "", StorageRecountJob.NextRun(clock.GetUtcNow()));
        await unitOfWork.SaveChangesAsync(ct);
    }
}