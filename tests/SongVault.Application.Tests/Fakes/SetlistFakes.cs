using SongVault.Application.Abstractions;
using SongVault.Application.Setlists;
using SongVault.Domain.Setlists;

namespace SongVault.Application.Tests.Fakes;

/// <summary>
/// Setlists en mémoire (#21) ; titres, versions, tonalité et BPM viennent du FakeSongRepository donné.
/// AudioDurations joue le rôle des aperçus audio prêts (#15) : durée en millisecondes par version.
/// </summary>
internal sealed class FakeSetlistRepository(FakeSongRepository? songs = null) : ISetlistRepository
{
    public List<Setlist> Setlists { get; } = [];
    public Dictionary<Guid, int> AudioDurations { get; } = [];

    public void Add(Setlist setlist) => Setlists.Add(setlist);

    public void Remove(Setlist setlist) => Setlists.Remove(setlist);

    public Task<Setlist?> GetAsync(Guid setlistId, CancellationToken ct)
        => Task.FromResult(Setlists.SingleOrDefault(s => s.Id == setlistId));

    public Task<IReadOnlyList<SetlistSummaryDto>> ListAsync(CancellationToken ct)
    {
        IReadOnlyList<SetlistSummaryDto> result = [.. Setlists
            .OrderBy(s => s.Date is null ? 0 : 1).ThenByDescending(s => s.Date).ThenByDescending(s => s.CreatedAt)
            .Select(s => new SetlistSummaryDto(s.Id, s.Name, s.Date, s.Venue, s.Items.Count))];
        return Task.FromResult(result);
    }

    public Task<SetlistDto?> GetDetailAsync(Guid setlistId, CancellationToken ct)
    {
        var s = Setlists.SingleOrDefault(x => x.Id == setlistId);
        if (s is null) return Task.FromResult<SetlistDto?>(null);

        var items = s.Items.Select(i =>
        {
            var song = songs?.Songs.SingleOrDefault(x => x.Id == i.SongId);
            var versions = (song?.Versions ?? []).Where(v => !v.IsDeleted).OrderBy(v => v.Number).ToList();
            var played = versions.FirstOrDefault(v => v.Id == i.VersionId) ?? versions.LastOrDefault();
            int? audio = played is not null && AudioDurations.TryGetValue(played.Id, out var ms) ? ms : null;
            return new SetlistItemDto(i.Position, i.SongId, song?.Title ?? "", i.VersionId, played?.Id,
                [.. versions.Select(v => new SetlistVersionOptionDto(v.Id, v.Number, v.Title))],
                played?.Key?.Value, played?.Bpm, i.Notes, i.DurationMs, audio, i.CommentBefore);
        });
        return Task.FromResult<SetlistDto?>(new SetlistDto(s.Id, s.Name, s.Date, s.Venue, s.CreatedAt, s.UpdatedAt, [.. items]));
    }

    public Task<IReadOnlyList<string>> ListNamesWithSongAsync(Guid songId, CancellationToken ct)
    {
        IReadOnlyList<string> names = [.. Setlists.Where(s => s.Items.Any(i => i.SongId == songId)).Select(s => s.Name).Order()];
        return Task.FromResult(names);
    }

    public Task<IReadOnlyList<string>> ListNamesWithVersionAsync(Guid versionId, CancellationToken ct)
    {
        IReadOnlyList<string> names = [.. Setlists.Where(s => s.Items.Any(i => i.VersionId == versionId)).Select(s => s.Name).Order()];
        return Task.FromResult(names);
    }
}