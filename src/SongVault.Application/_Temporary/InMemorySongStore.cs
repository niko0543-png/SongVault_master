using System.Collections.Concurrent;

using SongVault.Domain.Songs;

namespace SongVault.Application._Temporary;

public sealed class InMemorySongStore
{
    private readonly ConcurrentDictionary<Guid, Song> _songs = new();

    public void Add(Song song) => _songs[song.Id] = song;

    public Song? Find(Guid id) => _songs.TryGetValue(id, out var song) ? song : null;

    public IReadOnlyList<Song> List() => [.. _songs.Values.OrderBy(s => s.Title)];
}