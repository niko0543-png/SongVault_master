using SongVault.Application.Abstractions;
using SongVault.Infrastructure.Persistence;

namespace SongVault.Infrastructure.Jobs;

internal sealed class BackgroundJobScheduler(SongVaultDbContext db, TimeProvider clock) : IBackgroundJobs
{
    public void Enqueue(string type, string payload, DateTimeOffset runAfter)
    {
        var now = clock.GetUtcNow();
        db.BackgroundJobs.Add(new BackgroundJob
        {
            Id = Guid.CreateVersion7(now),
            Type = type,
            Payload = payload,
            Status = BackgroundJobStatus.Pending,
            CreatedAt = now,
            RunAfter = runAfter,
        });
    }
}