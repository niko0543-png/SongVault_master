using SongVault.Application.Abstractions;
using SongVault.Application.Common.Exceptions;

namespace SongVault.Application.Activity;

public sealed class ListActivityHandler(IActivityRepository events)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;

    /// <summary>Événements du groupe actif, du plus récent au plus ancien, après le curseur s'il y en a un.</summary>
    public async Task<ActivityPage> HandleAsync(ActivityCursor? after, int pageSize, CancellationToken ct)
    {
        var size = Math.Clamp(pageSize, 1, MaxPageSize);
        var items = await events.ListAsync(after, size + 1, ct);       // un de plus : reste-t-il une page ?
        if (items.Count <= size) return new ActivityPage(items, null);

        var page = items.Take(size).ToList();
        var last = page[^1];
        return new ActivityPage(page, new ActivityCursor(last.CreatedAt, last.Id).ToString());
    }
}

public sealed class MarkSongSeenHandler(
    ISongRepository songs, ISongViewRepository views, ICurrentUser user, TimeProvider clock)
{
    /// <summary>L'utilisateur vient d'ouvrir le morceau : son badge disparaît.</summary>
    public async Task HandleAsync(Guid songId, CancellationToken ct)
    {
        if (!await songs.ExistsAsync(songId, ct)) throw new NotFoundException("Song", songId);
        await views.MarkSeenAsync(songId, user.UserId, clock.GetUtcNow(), ct);
    }
}