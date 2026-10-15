using System.ComponentModel.DataAnnotations;

using SongVault.Application.Sharing;
using SongVault.Domain.Releases;
using SongVault.Domain.Sharing;

namespace SongVault.Api.Contracts.Sharing;

/// <summary>
/// Lien vers une version (TargetType Version, TargetId = id de la version) ou une sortie (Release).
/// ExpiresInDays : 1 à 90, 30 par défaut. Password : facultatif, 6 à 128 caractères.
/// </summary>
public sealed record CreateShareLinkRequest(
    [Required] ShareTargetType? TargetType,
    [Required] Guid? TargetId,
    [MaxLength(ShareLink.LabelMaxLength)] string? Label,
    [Range(ShareLink.MinLifetimeDays, ShareLink.MaxLifetimeDays)] int? ExpiresInDays,
    [StringLength(ShareLink.PasswordMaxLength, MinimumLength = ShareLink.PasswordMinLength)] string? Password);

public sealed record ShareLinkResponse(
    Guid Id, ShareTargetType TargetType, Guid TargetId, string? Label, bool HasPassword, ShareLinkStatus Status,
    DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, DateTimeOffset? RevokedAt, string? CreatedByEmail,
    int OpenCount, DateTimeOffset? LastOpenedAt)
{
    public static ShareLinkResponse From(ShareLinkDto d)
        => new(d.Id, d.TargetType, d.TargetId, d.Label, d.HasPassword, d.Status, d.CreatedAt, d.ExpiresAt, d.RevokedAt,
            d.CreatedByEmail, d.OpenCount, d.LastOpenedAt);
}

/// <summary>Lien créé. Url n'est renvoyée qu'ici : la base ne garde que l'empreinte du jeton.</summary>
public sealed record ShareLinkCreatedResponse(ShareLinkResponse Link, string Url)
{
    public static ShareLinkCreatedResponse From(ShareLinkCreatedDto d) => new(ShareLinkResponse.From(d.Link), d.Url);
}

/// <summary>Une ligne du journal. Visitor : même valeur pour un même visiteur sur ce lien, sans son adresse IP.</summary>
public sealed record ShareLinkAccessResponse(DateTimeOffset At, ShareAccessKind Kind, string? FileName, string? Visitor, string? UserAgent)
{
    public static ShareLinkAccessResponse From(ShareLinkAccessDto d) => new(d.At, d.Kind, d.FileName, d.Visitor, d.UserAgent);
}

public sealed record UnlockShareRequest([Required, MaxLength(ShareLink.PasswordMaxLength)] string Password);

public sealed record SharedFileResponse(Guid Id, string FileName, string ContentType, long SizeBytes);

public sealed record SharedTrackResponse(
    int Number, string SongTitle, string? Artist, int VersionNumber, string VersionTitle, IReadOnlyList<SharedFileResponse> Files);

/// <summary>Page publique d'un lien : le groupe, la cible et ses fichiers audio, rien d'autre.</summary>
public sealed record SharedContentResponse(
    ShareTargetType TargetType, string BandName, string Title, ReleaseType? ReleaseType, DateOnly? ReleaseDate,
    bool HasCover, DateTimeOffset ExpiresAt, IReadOnlyList<SharedTrackResponse> Tracks)
{
    public static SharedContentResponse From(SharedContentDto d)
        => new(d.TargetType, d.BandName, d.Title, d.ReleaseType, d.ReleaseDate, d.HasCover, d.ExpiresAt,
            [.. d.Tracks.Select(t => new SharedTrackResponse(t.Number, t.SongTitle, t.Artist, t.VersionNumber, t.VersionTitle,
                [.. t.Files.Select(f => new SharedFileResponse(f.Id, f.FileName, f.ContentType, f.SizeBytes))]))]);
}