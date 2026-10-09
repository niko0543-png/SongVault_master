using SongVault.Application.Abstractions;
using SongVault.Application.Common;
using SongVault.Application.Common.Exceptions;
using SongVault.Application.Files;
using SongVault.Application.Songs;
using SongVault.Application.Versions;
using SongVault.Domain.Songs;

namespace SongVault.Application.Tests.Fakes;

/// <summary>Repository en mémoire : aucune base de données, juste une liste.</summary>
internal sealed class FakeSongRepository : ISongRepository
{
    public List<Song> Songs { get; } = [];

    // Ce que le dernier appel à ListAsync a reçu (utilisé par les tests de pagination et de recherche)
    public (int Page, int PageSize)? LastListRequest { get; private set; }
    public string? LastSearch { get; private set; }
    public SongVersionStatus? LastStatus { get; private set; }

    public void Add(Song song) => Songs.Add(song);

    public void Remove(Song song) => Songs.Remove(song);

    public Task<Song?> GetByIdAsync(Guid id, CancellationToken ct)
        => Task.FromResult(Songs.SingleOrDefault(s => s.Id == id));

    public Task<PagedResult<SongDto>> ListAsync(
        int page, int pageSize, string? search, SongVersionStatus? status, CancellationToken ct)
    {
        LastListRequest = (page, pageSize);
        LastSearch = search;
        LastStatus = status;

        // Même logique que le vrai repository, mais en LINQ to Objects
        IEnumerable<Song> query = Songs;
        if (search is not null)
            query = query.Where(s =>
                s.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (s.Artist?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        if (status is not null)
            query = query.Where(s =>
                s.Versions.OrderByDescending(v => v.Number).Select(v => (SongVersionStatus?)v.Status).FirstOrDefault() == status);

        var filtered = query.ToList();
        IReadOnlyList<SongDto> items = [.. filtered
            .OrderBy(s => s.Title)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(SongDto.From)];

        return Task.FromResult(new PagedResult<SongDto>(items, page, pageSize, filtered.Count));
    }

    public Task<Song?> GetWithVersionAsync(Guid songId, Guid versionId, CancellationToken ct)
        => GetByIdAsync(songId, ct);     // en mémoire, toutes les versions sont déjà « chargées »

    public Task<bool> ExistsAsync(Guid songId, CancellationToken ct)
        => Task.FromResult(Songs.Any(s => s.Id == songId));

    public Task<IReadOnlyList<SongVersionSummaryDto>> ListVersionsAsync(Guid songId, CancellationToken ct)
    {
        var song = Songs.SingleOrDefault(s => s.Id == songId);
        IReadOnlyList<SongVersionSummaryDto> result = song is null
            ? []
            : [.. song.Versions.OrderBy(v => v.Number)
                  .Select(v => new SongVersionSummaryDto(v.Id, v.Number, v.Title, v.Status, v.CreatedAt))];
        return Task.FromResult(result);
    }

    public Task<SongVersionDto?> GetVersionAsync(Guid songId, Guid versionId, CancellationToken ct)
    {
        var version = Songs.SingleOrDefault(s => s.Id == songId)?.FindVersion(versionId);
        return Task.FromResult(version is null ? null : SongVersionDto.From(version));
    }

    public Task<StoredFileInfo?> GetFileAsync(Guid songId, Guid versionId, Guid fileId, CancellationToken ct)
    {
        var file = Songs.SingleOrDefault(s => s.Id == songId)?
            .FindVersion(versionId)?
            .Files.SingleOrDefault(f => f.Id == fileId);
        return Task.FromResult(file is null ? null : new StoredFileInfo(file.StorageKey, file.ContentType, file.OriginalFileName));
    }

    public Task<IReadOnlyList<string>> ListStorageKeysAsync(Guid songId, CancellationToken ct)
    {
        IReadOnlyList<string> keys = [.. Songs
            .Where(s => s.Id == songId)
            .SelectMany(s => s.Versions)
            .SelectMany(v => v.Files)
            .Select(f => f.StorageKey)];
        return Task.FromResult(keys);
    }
}

/// <summary>Unité de travail factice : compte les enregistrements et peut simuler des conflits.</summary>
internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }
    public int DiscardCount { get; private set; }
    public int FailuresToSimulate { get; set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        if (FailuresToSimulate-- > 0) throw new ConcurrencyConflictException();
        return Task.FromResult(1);
    }

    public void DiscardChanges() => DiscardCount++;
}

/// <summary>Horloge figée : les tests sont déterministes.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

/// <summary>Stockage de fichiers en mémoire.</summary>
internal sealed class FakeFileStorageService : IFileStorageService
{
    public Dictionary<string, byte[]> Files { get; } = [];

    public async Task SaveAsync(string storageKey, Stream content, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        Files[storageKey] = ms.ToArray();
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct)
        => Task.FromResult<Stream>(new MemoryStream(Files[storageKey]));

    public Task DeleteAsync(string storageKey, CancellationToken ct)
    {
        Files.Remove(storageKey);
        return Task.CompletedTask;
    }
}

internal sealed class FakeCurrentUser(string userId = "owner-1", string? email = "owner-1@test.local") : ICurrentUser
{
    public string UserId { get; } = userId;
    public string? Email { get; } = email;
}