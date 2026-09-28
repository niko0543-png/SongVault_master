using SongVault.Application.Abstractions;
using SongVault.Application.Common;
using SongVault.Application.Songs;
using SongVault.Application.Versions;
using SongVault.Domain.Songs;

namespace SongVault.Application.Tests.Fakes;

internal sealed class FakeSongRepository : ISongRepository
{
    public List<Song> Songs { get; } = [];

    public void Add(Song song) => Songs.Add(song);
    public void Remove(Song song) => Songs.Remove(song);

    public Task<Song?> GetByIdAsync(Guid id, CancellationToken ct)
        => Task.FromResult(Songs.SingleOrDefault(s => s.Id == id));

    public (int Page, int PageSize)? LastListRequest { get; private set; }

    public Task<PagedResult<SongDto>> ListAsync(int page, int pageSize, CancellationToken ct)
    {
        LastListRequest = (page, pageSize);
        IReadOnlyList<SongDto> items = [.. Songs.Skip((page - 1) * pageSize).Take(pageSize).Select(SongDto.From)];
        return Task.FromResult(new PagedResult<SongDto>(items, page, pageSize, Songs.Count));
    }

    public Task<Song?> GetWithVersionAsync(Guid songId, Guid versionId, CancellationToken ct)
        => Task.FromResult(Songs.SingleOrDefault(s => s.Id == songId && s.Versions.Any(v => v.Id == versionId)));

    public Task<bool> ExistsAsync(Guid songId, CancellationToken ct)
        => Task.FromResult(Songs.Any(s => s.Id == songId));

    public Task<IReadOnlyList<SongVersionSummaryDto>> ListVersionsAsync(Guid songId, CancellationToken ct)
        => Task.FromResult((IReadOnlyList<SongVersionSummaryDto>)Songs
            .Where(s => s.Id == songId)
            .SelectMany(s => s.Versions)
            .OrderBy(v => v.Number)
            .Select(v => new SongVersionSummaryDto(v.Id, v.Number, v.Title, v.Status, v.CreatedAt))
            .ToList());

    public Task<SongVersionDto?> GetVersionAsync(Guid songId, Guid versionId, CancellationToken ct)
        => Task.FromResult(Songs
            .Where(s => s.Id == songId)
            .SelectMany(s => s.Versions)
            .Where(v => v.Id == versionId)
            .Select(v => new SongVersionDto(v.Id, v.SongId, v.Number, v.Title, v.Status, v.Notes, v.Lyrics, v.CreatedAt, v.UpdatedAt))
            .FirstOrDefault());
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