using SongVault.Application.Abstractions;
using SongVault.Application.Activity;
using SongVault.Application.Common;
using SongVault.Application.Common.Exceptions;
using SongVault.Application.Files;
using SongVault.Application.Releases;
using SongVault.Application.Songs;
using SongVault.Application.Versions;
using SongVault.Domain.Bands;
using SongVault.Domain.Files;
using SongVault.Domain.Releases;
using SongVault.Domain.Songs;
using SongVault.Domain.Activity;

namespace SongVault.Application.Tests.Fakes;

/// <summary>Repository en mémoire : aucune base de données, juste une liste.</summary>
internal sealed class FakeSongRepository : ISongRepository
{
    public List<Song> Songs { get; } = [];
    /// <summary>#17 : état de l'analyse antivirus de chaque fichier, rendu par GetFileAsync (absent : null).</summary>
    public Dictionary<Guid, FileScanStatus> Scans { get; } = [];

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
        return Task.FromResult(file is null ? null : new StoredFileInfo(file.StorageKey, file.ContentType, file.OriginalFileName,
    Scans.TryGetValue(file.Id, out var scan) ? scan : null));
    }

    public Task<SongFileLabel?> GetFileLabelAsync(Guid songId, Guid versionId, Guid fileId, CancellationToken ct)
    {
        var song = Songs.SingleOrDefault(s => s.Id == songId);
        var version = song?.FindVersion(versionId);
        var file = version?.Files.SingleOrDefault(f => f.Id == fileId);
        return Task.FromResult(file is null ? null : new SongFileLabel(
            song!.Id, song.Title, version!.Id, version.Number, version.Title, file.Id, file.OriginalFileName, file.FileType));
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

    public Task<StoredFileInfo?> GetListenFileAsync(Guid songId, Guid versionId, Guid fileId, CancellationToken ct)
       => GetFileAsync(songId, versionId, fileId, ct);         // pas d'aperçu en mémoire : l'original

    public Task<AudioWaveformDto?> GetWaveformAsync(Guid songId, Guid versionId, Guid fileId, CancellationToken ct)
        => Task.FromResult<AudioWaveformDto?>(null);

    public Task<IReadOnlyDictionary<Guid, Guid>> GetVersionSongIdsAsync(IReadOnlyCollection<Guid> versionIds, CancellationToken ct)
    {
        IReadOnlyDictionary<Guid, Guid> result = Songs.SelectMany(s => s.Versions)
            .Where(v => versionIds.Contains(v.Id))
            .ToDictionary(v => v.Id, v => v.SongId);
        return Task.FromResult(result);
    }
}

/// <summary>Sorties en mémoire ; les titres et versions des pistes viennent du FakeSongRepository donné.</summary>
internal sealed class FakeReleaseRepository(FakeSongRepository? songs = null) : IReleaseRepository
{
    public List<Release> Releases { get; } = [];

    public void Add(Release release) => Releases.Add(release);

    public void Remove(Release release) => Releases.Remove(release);

    public Task<Release?> GetAsync(Guid releaseId, CancellationToken ct)
        => Task.FromResult(Releases.SingleOrDefault(r => r.Id == releaseId));

    public Task<IReadOnlyList<ReleaseSummaryDto>> ListAsync(CancellationToken ct)
    {
        IReadOnlyList<ReleaseSummaryDto> result = [.. Releases
            .OrderBy(r => r.ReleaseDate is null ? 0 : 1).ThenByDescending(r => r.ReleaseDate).ThenByDescending(r => r.CreatedAt)
            .Select(r => new ReleaseSummaryDto(r.Id, r.Title, r.Type, r.ReleaseDate, r.CoverStorageKey is not null, r.Tracks.Count))];
        return Task.FromResult(result);
    }

    public Task<ReleaseDto?> GetDetailAsync(Guid releaseId, CancellationToken ct)
    {
        var r = Releases.SingleOrDefault(x => x.Id == releaseId);
        if (r is null) return Task.FromResult<ReleaseDto?>(null);

        var tracks = r.Tracks.Select(t =>
        {
            var song = songs?.Songs.SingleOrDefault(s => s.Id == t.SongId);
            return new ReleaseTrackDto(t.TrackNumber, t.SongId, song?.Title ?? "", t.VersionId,
                [.. (song?.Versions ?? []).OrderBy(v => v.Number)
                    .Select(v => new ReleaseVersionOptionDto(v.Id, v.Number, v.Title, v.Status))]);
        });
        return Task.FromResult<ReleaseDto?>(new ReleaseDto(r.Id, r.Title, r.Type, r.ReleaseDate,
            r.CoverStorageKey is not null, r.CreatedAt, r.UpdatedAt, [.. tracks]));
    }

    public Task<IReadOnlyList<string>> ListTitlesWithSongAsync(Guid songId, CancellationToken ct)
    {
        IReadOnlyList<string> titles = [.. Releases.Where(r => r.Tracks.Any(t => t.SongId == songId))
            .Select(r => r.Title).Order()];
        return Task.FromResult(titles);
    }
}

/// <summary>Fil d'activité en mémoire : Events reçoit les écritures, Items sert les lectures.</summary>
internal sealed class FakeActivityRepository : IActivityRepository
{
    public List<ActivityEvent> Events { get; } = [];
    public List<ActivityItemDto> Items { get; } = [];

    public void Add(ActivityEvent activityEvent) => Events.Add(activityEvent);

    public Task<IReadOnlyList<ActivityItemDto>> ListAsync(ActivityCursor? after, int take, CancellationToken ct)
    {
        IReadOnlyList<ActivityItemDto> page = [.. Items
            .Where(i => after is not { } c || i.CreatedAt < c.CreatedAt || (i.CreatedAt == c.CreatedAt && i.Id < c.Id))
            .OrderByDescending(i => i.CreatedAt).ThenByDescending(i => i.Id)
            .Take(take)];
        return Task.FromResult(page);
    }
}

/// <summary>Ouvertures des morceaux en mémoire.</summary>
internal sealed class FakeSongViewRepository : ISongViewRepository
{
    public Dictionary<(Guid SongId, string UserId), DateTimeOffset> SeenAt { get; } = [];

    public Task MarkSeenAsync(Guid songId, string userId, DateTimeOffset now, CancellationToken ct)
    {
        SeenAt[(songId, userId)] = now;
        return Task.CompletedTask;
    }
}

/// <summary>Unité de travail factice : compte les enregistrements et peut simuler des conflits.</summary>
internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }
    public int DiscardCount { get; private set; }
    public int FailuresToSimulate { get; set; }

    public int TransactionCount { get; private set; }

    /// <summary>Pas de vraie transaction en mémoire : compte l'appel et exécute work.</summary>
    public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken ct)
    {
        TransactionCount++;
        return work(ct);
    }

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
        => Files.TryGetValue(storageKey, out var bytes)
            ? Task.FromResult<Stream>(new MemoryStream(bytes))
            : throw new FileNotFoundException("Fichier absent du stockage.", storageKey);

    public Task DeleteAsync(string storageKey, CancellationToken ct)
    {
        Files.Remove(storageKey);
        return Task.CompletedTask;
    }


    public Task<long?> GetLengthAsync(string storageKey, CancellationToken ct)
        => Task.FromResult(Files.TryGetValue(storageKey, out var bytes) ? bytes.Length : (long?)null);

    /// <summary>Si renseigné, le faux stockage se comporte comme Azure Blob : il produit des URL.</summary>
    public Uri? BaseUrl { get; set; }

    public DirectUpload? CreateUploadUrl(string storageKey, DateTimeOffset expiresAt)
        => BaseUrl is null ? null : new DirectUpload(new Uri(BaseUrl, storageKey), new Dictionary<string, string>());

    public Uri? CreateDownloadUrl(string storageKey, string contentType, string? attachmentName)
        => BaseUrl is null ? null : new Uri(BaseUrl, storageKey);
}

internal sealed class FakeCurrentUser(string userId = "owner-1", string? email = "owner-1@test.local") : ICurrentUser
{
    public string UserId { get; } = userId;
    public string? Email { get; } = email;
}

/// <summary>Groupe actif figé, comme si [BandScoped] avait validé l'adhésion.</summary>
internal sealed class FakeBandContext(Guid bandId) : IBandContext
{
    public Guid? BandId { get; } = bandId;
    public BandRole? Role { get; } = BandRole.Owner;
    public Guid RequiredBandId { get; } = bandId;
}