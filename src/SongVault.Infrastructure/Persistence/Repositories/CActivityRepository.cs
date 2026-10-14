using Microsoft.EntityFrameworkCore;

using SongVault.Application.Abstractions;
using SongVault.Application.Activity;
using SongVault.Domain.Activity;

namespace SongVault.Infrastructure.Persistence.Repositories;

/// <summary>Toutes les lectures partent de BandEvents : seulement le groupe actif, vérifié par [BandScoped].</summary>
internal sealed class ActivityRepository(SongVaultDbContext db, IBandContext bandContext) : IActivityRepository
{
    private IQueryable<ActivityEvent> BandEvents => db.ActivityEvents.Where(e => e.BandId == bandContext.RequiredBandId);

    public void Add(ActivityEvent activityEvent) => db.ActivityEvents.Add(activityEvent);

    public async Task<IReadOnlyList<ActivityItemDto>> ListAsync(ActivityCursor? after, int take, CancellationToken ct)
    {
        var query = BandEvents.AsNoTracking();
        if (after is { } c)
            query = query.Where(e => e.CreatedAt < c.CreatedAt || (e.CreatedAt == c.CreatedAt && e.Id < c.Id));

        return await query
            .OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id)
            .Take(take)
            .Select(e => new ActivityItemDto(
                e.Id, e.Type, e.ActorId,
                db.Users.Where(u => u.Id == e.ActorId).Select(u => u.Email).FirstOrDefault(),
                e.SongId, db.Songs.Any(s => s.Id == e.SongId), e.VersionId, e.EntityId,
                e.Details.SongTitle, e.Details.VersionNumber, e.Details.VersionTitle, e.Details.FileName,
                e.CreatedAt))
            .ToListAsync(ct);
    }
}