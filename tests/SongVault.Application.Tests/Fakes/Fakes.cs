using SongVault.Application.Abstractions;
using SongVault.Application.Songs;
using SongVault.Domain.Songs;

namespace SongVault.Application.Tests.Fakes;

internal sealed class FakeSongRepository : ISongRepository
{
    public List<Song> Songs { get; } = [];

    public void Add(Song song) => Songs.Add(song);
    public void Remove(Song song) => Songs.Remove(song);

    public Task<Song?> GetByIdAsync(Guid id, CancellationToken ct)
        => Task.FromResult(Songs.SingleOrDefault(s => s.Id == id));

    public Task<IReadOnlyList<SongDto>> ListAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<SongDto>>([.. Songs.Select(SongDto.From)]);
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.FromResult(1);
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}