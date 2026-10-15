using SongVault.Application.Abstractions;
using SongVault.Application.Files;
using SongVault.Application.Sharing;
using SongVault.Domain.Sharing;

namespace SongVault.Application.Tests.Fakes;

/// <summary>Liens en mémoire. Targets : cibles qui existent dans le groupe ; Files : fichiers audio de chaque lien.</summary>
internal sealed class FakeShareLinkRepository : IShareLinkRepository
{
    public List<ShareLink> Links { get; } = [];
    public List<ShareLinkAccess> Accesses { get; } = [];
    public HashSet<(Guid BandId, Guid TargetId)> Targets { get; } = [];
    public Dictionary<(Guid LinkId, Guid FileId), StoredFileInfo> Files { get; } = [];

    public void Add(ShareLink link) => Links.Add(link);

    public void Remove(ShareLink link) => Links.Remove(link);

    public void AddAccess(ShareLinkAccess access) => Accesses.Add(access);

    public Task<ShareLink?> GetByTokenHashAsync(byte[] tokenHash, CancellationToken ct)
        => Task.FromResult(Links.SingleOrDefault(l => l.TokenHash.SequenceEqual(tokenHash)));

    public Task<ShareLink?> GetInBandAsync(Guid bandId, Guid linkId, CancellationToken ct)
        => Task.FromResult(Links.SingleOrDefault(l => l.Id == linkId && l.BandId == bandId));

    public Task<bool> TargetExistsAsync(Guid bandId, ShareTargetType type, Guid targetId, CancellationToken ct)
        => Task.FromResult(Targets.Contains((bandId, targetId)));

    public Task<IReadOnlyList<ShareLinkDto>> ListAsync(
        Guid bandId, ShareTargetType type, Guid targetId, DateTimeOffset now, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<ShareLinkDto>>([.. Links
            .Where(l => l.BandId == bandId && (l.VersionId ?? l.ReleaseId) == targetId)
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new ShareLinkDto(l.Id, l.TargetType, targetId, l.Label, l.HasPassword, l.StatusAt(now),
                l.CreatedAt, l.ExpiresAt, l.RevokedAt, null,
                Accesses.Count(a => a.ShareLinkId == l.Id && a.Kind == ShareAccessKind.Opened), null))]);

    public Task<IReadOnlyList<ShareLinkAccessDto>> ListAccessesAsync(Guid linkId, int limit, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<ShareLinkAccessDto>>([.. Accesses
            .Where(a => a.ShareLinkId == linkId)
            .Reverse()
            .Take(limit)
            .Select(a => new ShareLinkAccessDto(a.At, a.Kind, null, null, a.UserAgent))]);

    public Task<SharedContentDto?> ReadContentAsync(ShareLink link, CancellationToken ct)
        => Task.FromResult<SharedContentDto?>(new SharedContentDto(link.TargetType, "Les Nocturnes", "Aube", null, null,
            false, link.ExpiresAt, []));

    public Task<StoredFileInfo?> GetSharedFileAsync(ShareLink link, Guid fileId, CancellationToken ct)
        => Task.FromResult(Files.GetValueOrDefault((link.Id, fileId)));

    public Task<StoredFileInfo?> GetSharedCoverAsync(ShareLink link, CancellationToken ct)
        => Task.FromResult<StoredFileInfo?>(null);
}

/// <summary>Hachage factice, lisible dans les assertions.</summary>
internal sealed class FakeSharePasswordHasher : ISharePasswordHasher
{
    public string Hash(string password) => $"hash:{password}";

    public bool Verify(string passwordHash, string password) => passwordHash == $"hash:{password}";
}

/// <summary>Jeton de déverrouillage en clair : « identifiant|fin ».</summary>
internal sealed class FakeShareUnlockTokens : IShareUnlockTokens
{
    public string Create(Guid linkId, DateTimeOffset expiresAt) => $"{linkId}|{expiresAt.ToUnixTimeSeconds()}";

    public bool TryRead(string token, out Guid linkId, out DateTimeOffset expiresAt)
    {
        var parts = token.Split('|');
        linkId = parts.Length == 2 && Guid.TryParse(parts[0], out var id) ? id : Guid.Empty;
        expiresAt = parts.Length == 2 && long.TryParse(parts[1], out var s) ? DateTimeOffset.FromUnixTimeSeconds(s) : default;
        return linkId != Guid.Empty;
    }
}