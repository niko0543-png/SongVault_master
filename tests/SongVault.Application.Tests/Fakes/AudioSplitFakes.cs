using SongVault.Application.Abstractions;
using SongVault.Application.Files.Splits;
using SongVault.Domain.Files;
using SongVault.Domain.Songs;

namespace SongVault.Application.Tests.Fakes;

/// <summary>Découpages en mémoire, au-dessus des morceaux du faux dépôt des morceaux ; Sources : fichiers à découper.</summary>
internal sealed class FakeAudioSplitRepository(FakeSongRepository songs) : IAudioSplitRepository
{
    public List<AudioSplit> Splits { get; } = [];
    public Dictionary<Guid, AudioSplitSource> Sources { get; } = [];

    public void Add(AudioSplit split) => Splits.Add(split);

    public Task<AudioSplit?> GetAsync(Guid splitId, CancellationToken ct)
        => Task.FromResult(Splits.SingleOrDefault(s => s.Id == splitId));

    public Task<AudioSplit?> GetLatestAsync(Guid bandId, Guid sourceFileId, CancellationToken ct)
        => Task.FromResult(Splits.Where(s => s.BandId == bandId && s.SourceFileId == sourceFileId)
            .OrderByDescending(s => s.RequestedAt).FirstOrDefault());

    public Task<AudioSplitSource?> GetSourceAsync(Guid bandId, Guid songId, Guid versionId, Guid fileId, CancellationToken ct)
        => Task.FromResult(Sources.GetValueOrDefault(fileId));

    public Task<IReadOnlySet<Guid>> ListLiveSongIdsAsync(Guid bandId, IReadOnlyCollection<Guid> songIds, CancellationToken ct)
        => Task.FromResult<IReadOnlySet<Guid>>(Live(bandId).Where(s => songIds.Contains(s.Id)).Select(s => s.Id).ToHashSet());

    public Task<Song?> GetSongAsync(Guid bandId, Guid songId, CancellationToken ct)
        => Task.FromResult(Live(bandId).SingleOrDefault(s => s.Id == songId));

    public void AddSong(Song song) => songs.Add(song);

    private IEnumerable<Song> Live(Guid bandId) => songs.Songs.Where(s => s.BandId == bandId && !s.IsDeleted);
}
