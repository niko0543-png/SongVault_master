using System.ComponentModel.DataAnnotations;

using SongVault.Application.Releases;
using SongVault.Domain.Releases;

namespace SongVault.Api.Contracts.Releases;

/// <summary>
/// Les infos de sortie de la dernière version, au statut Final (#38). ReleaseId vide : un single au titre du morceau
/// est créé ; sinon le morceau rejoint cette sortie. Keep : All garde toutes les versions, LatestOnly met les autres à la
/// corbeille ; PurgeNow (Owner) les efface tout de suite pour libérer la place.
/// </summary>
public sealed record ReleaseSongRequest(
    [Required] Guid? VersionId,
    Guid? ReleaseId,
    DateOnly? ReleaseDate,
    [MaxLength(Release.LabelMaxLength)] string? Label,
    [MaxLength(ReleaseDistributionRequest.UpcInputMaxLength)] string? Upc,
    [MaxLength(Release.MaxStreamingLinks)] IReadOnlyList<string>? StreamingLinks,
    [Required] KeepVersions? Keep,
    bool PurgeNow = false)
{
    public ReleaseSongCommand ToCommand(Guid songId)
        => new(songId, VersionId!.Value, ReleaseId, ReleaseDate, Label, Upc, StreamingLinks ?? [], Keep!.Value, PurgeNow);
}

/// <summary>Label, code-barres UPC/EAN (vide = aucun) et liens d'écoute d'une sortie, qui remplacent les anciens.</summary>
public sealed record ReleaseDistributionRequest(
    [MaxLength(Release.LabelMaxLength)] string? Label,
    [MaxLength(ReleaseDistributionRequest.UpcInputMaxLength)] string? Upc,
    [Required, MaxLength(Release.MaxStreamingLinks)] IReadOnlyList<string> StreamingLinks)
{
    /// <summary>La saisie peut garder espaces et tirets : on vérifie le format après les avoir ôtés.</summary>
    public const int UpcInputMaxLength = 30;
}

public sealed record StreamingLinkResponse(StreamingPlatform Platform, string Url);

/// <summary>Une version gardée malgré « seulement la dernière » : les sorties et setlists qui l'utilisent.</summary>
public sealed record KeptVersionResponse(int Number, IReadOnlyList<string> Releases, IReadOnlyList<string> Setlists);

/// <summary>La sortie du morceau ; RemovedVersions : numéros des versions écartées ; KeptVersions : celles qui restent, et pourquoi.</summary>
public sealed record SongReleaseResponse(
    Guid ReleaseId, IReadOnlyList<int> RemovedVersions, IReadOnlyList<KeptVersionResponse> KeptVersions, bool Purged)
{
    public static SongReleaseResponse From(SongReleaseResultDto d)
        => new(d.ReleaseId, d.RemovedVersions, [.. d.KeptVersions.Select(k => new KeptVersionResponse(k.Number, k.Releases, k.Setlists))], d.Purged);
}
