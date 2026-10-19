using SongVault.Application.Abstractions;
using SongVault.Application.Trash;
using SongVault.Domain.Files;
using SongVault.Domain.Songs;
using SongVault.Domain.Trash;

namespace SongVault.Application.Tests.Fakes;

/// <summary>
/// Corbeille en mémoire (#20), lue dans le FakeSongRepository donné. Un élément purgé ne disparaît pas de l'agrégat
/// (le domaine n'a pas de quoi l'en ôter) : il va dans Purged, et la corbeille ne le voit plus.
/// </summary>
internal sealed class FakeTrashRepository(FakeSongRepository songs) : ITrashRepository
{
    public List<(TrashItemKind Kind, Guid Id)> Purged { get; } = [];
    /// <summary>Appelé au moment où les lignes partent : les tests y regardent ce qui est déjà fait.</summary>
    public Action? OnDelete { get; set; }

    private bool IsPurged(Guid id) => Purged.Any(p => p.Id == id);
    private IEnumerable<Song> Present => songs.Songs.Where(s => !IsPurged(s.Id));
    private IEnumerable<(Song Song, SongVersion Version)> Versions
        => Present.SelectMany(s => s.Versions.Where(v => !IsPurged(v.Id)).Select(v => (s, v)));
    private IEnumerable<(Song Song, SongVersion Version, SongFile File)> Files
        => Versions.SelectMany(x => x.Version.Files.Where(f => !IsPurged(f.Id)).Select(f => (x.Song, x.Version, f)));

    public Task<IReadOnlyList<TrashItemDto>> ListAsync(CancellationToken ct)
    {
        IEnumerable<TrashItemDto> items = [
            .. Present.Where(s => s.IsDeleted).Select(s => new TrashItemDto(TrashItemKind.Song, s.Id, s.Id, s.Title,
                null, null, null, s.Versions.SelectMany(v => v.Files).Sum(f => f.SizeBytes), s.DeletedAt!.Value, s.DeletedById, null)),
            .. Versions.Where(x => !x.Song.IsDeleted && x.Version.IsDeleted).Select(x => new TrashItemDto(TrashItemKind.Version,
                x.Version.Id, x.Song.Id, x.Song.Title, x.Version.Number, x.Version.Title, null, x.Version.Files.Sum(f => f.SizeBytes),
                x.Version.DeletedAt!.Value, x.Version.DeletedById, null)),
            .. Files.Where(x => !x.Song.IsDeleted && !x.Version.IsDeleted && x.File.IsDeleted).Select(x => new TrashItemDto(
                TrashItemKind.File, x.File.Id, x.Song.Id, x.Song.Title, x.Version.Number, x.Version.Title, x.File.OriginalFileName,
                x.File.SizeBytes, x.File.DeletedAt!.Value, x.File.DeletedById, null)),
        ];
        IReadOnlyList<TrashItemDto> list = [.. items.OrderByDescending(i => i.DeletedAt)];
        return Task.FromResult(list);
    }

    public Task<Song?> GetSongAsync(TrashItemKind kind, Guid itemId, CancellationToken ct)
        => Task.FromResult(kind switch
        {
            TrashItemKind.Song => Present.SingleOrDefault(s => s.Id == itemId),
            TrashItemKind.Version => Versions.SingleOrDefault(x => x.Version.Id == itemId).Song,
            _ => Files.SingleOrDefault(x => x.File.Id == itemId).Song,
        });

    public Task<IReadOnlyList<TrashEntry>> ListExpiredAsync(DateTimeOffset deletedUpTo, CancellationToken ct)
    {
        IReadOnlyList<TrashEntry> list = [
            .. Present.Where(s => s.DeletedAt <= deletedUpTo).Select(s => new TrashEntry(TrashItemKind.Song, s.Id)),
            .. Versions.Where(x => x.Version.DeletedAt <= deletedUpTo).Select(x => new TrashEntry(TrashItemKind.Version, x.Version.Id)),
            .. Files.Where(x => x.File.DeletedAt <= deletedUpTo).Select(x => new TrashEntry(TrashItemKind.File, x.File.Id)),
        ];
        return Task.FromResult(list);
    }

    public Task<PurgeTarget?> GetPurgeTargetAsync(TrashItemKind kind, Guid itemId, CancellationToken ct)
    {
        PurgeTarget? target = kind switch
        {
            TrashItemKind.Song => Present.Where(s => s.Id == itemId && s.IsDeleted)
                .Select(s => new PurgeTarget(s.BandId, s.Title, [.. s.Versions.SelectMany(v => v.Files).Select(f => f.StorageKey)]))
                .SingleOrDefault(),
            TrashItemKind.Version => Versions.Where(x => x.Version.Id == itemId && x.Version.IsDeleted)
                .Select(x => new PurgeTarget(x.Song.BandId, TrashPurger.VersionSubject(x.Song.Title, x.Version.Number),
                    [.. x.Version.Files.Select(f => f.StorageKey)]))
                .SingleOrDefault(),
            _ => Files.Where(x => x.File.Id == itemId && x.File.IsDeleted)
                .Select(x => new PurgeTarget(x.Song.BandId, $"{x.Song.Title} — v{x.Version.Number} — {x.File.OriginalFileName}",
                    [x.File.StorageKey]))
                .SingleOrDefault(),
        };
        return Task.FromResult(target);
    }

    public Task DeleteAsync(TrashItemKind kind, Guid itemId, CancellationToken ct)
    {
        OnDelete?.Invoke();
        Purged.Add((kind, itemId));
        return Task.CompletedTask;
    }
}