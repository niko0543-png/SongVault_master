using SongVault.Domain.Releases;
using SongVault.Domain.Sharing;

namespace SongVault.Application.Sharing;

/// <summary>
/// Un lien, tel que les membres le voient. Le jeton n'y est jamais : la base n'en garde que l'empreinte.
/// OpenCount : ouvertures de la page (les écoutes et les mauvais mots de passe sont dans le journal).
/// </summary>
public sealed record ShareLinkDto(
    Guid Id, ShareTargetType TargetType, Guid TargetId, string? Label, bool HasPassword, ShareLinkStatus Status,
    DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, DateTimeOffset? RevokedAt, string? CreatedByEmail,
    int OpenCount, DateTimeOffset? LastOpenedAt);

/// <summary>Résultat d'une création : le lien complet, montré une seule fois.</summary>
public sealed record ShareLinkCreatedDto(ShareLinkDto Link, string Url);

/// <summary>
/// Une ligne du journal. Visitor : les 8 premiers caractères de l'empreinte de l'IP, pour reconnaître
/// un même visiteur sur ce lien sans connaître son adresse.
/// </summary>
public sealed record ShareLinkAccessDto(DateTimeOffset At, ShareAccessKind Kind, string? FileName, string? Visitor, string? UserAgent);

/// <summary>Ce que voit la personne qui ouvre le lien, sans compte : rien d'autre que la cible.</summary>
public sealed record SharedContentDto(
    ShareTargetType TargetType, string BandName, string Title, ReleaseType? ReleaseType, DateOnly? ReleaseDate,
    bool HasCover, DateTimeOffset ExpiresAt, IReadOnlyList<SharedTrackDto> Tracks);

/// <summary>Une piste : une seule pour un lien de version, une par piste de la sortie sinon.</summary>
public sealed record SharedTrackDto(
    int Number, string SongTitle, string? Artist, int VersionNumber, string VersionTitle, IReadOnlyList<SharedFileDto> Files);

public sealed record SharedFileDto(Guid Id, string FileName, string ContentType, long SizeBytes);

/// <summary>Mot de passe accepté : jeton à poser en cookie, valable jusqu'à ExpiresAt.</summary>
public sealed record ShareUnlockDto(string UnlockToken, DateTimeOffset ExpiresAt);

/// <summary>Ce que l'API sait du visiteur, pour le journal. L'IP n'est jamais enregistrée telle quelle.</summary>
public sealed record ShareVisitor(string? IpAddress, string? UserAgent);