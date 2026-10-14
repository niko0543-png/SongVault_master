using SongVault.Domain.Releases;
using SongVault.Domain.Songs;

namespace SongVault.Application.Releases;

/// <summary>Une sortie dans la liste du groupe.</summary>
public sealed record ReleaseSummaryDto(
    Guid Id, string Title, ReleaseType Type, DateOnly? ReleaseDate, bool HasCover, int TrackCount);

/// <summary>Une sortie et ses pistes, dans l'ordre.</summary>
public sealed record ReleaseDto(
    Guid Id, string Title, ReleaseType Type, DateOnly? ReleaseDate, bool HasCover,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, IReadOnlyList<ReleaseTrackDto> Tracks)
{
    /// <summary>Sortie qui vient d'être créée : pas encore de piste.</summary>
    public static ReleaseDto FromNew(Release r)
        => new(r.Id, r.Title, r.Type, r.ReleaseDate, r.CoverStorageKey is not null, r.CreatedAt, r.UpdatedAt, []);
}

/// <summary>
/// Une piste. Versions liste toutes les versions du morceau, pour choisir celle qui figure sur la sortie ;
/// VersionId est la version choisie.
/// </summary>
public sealed record ReleaseTrackDto(
    int TrackNumber, Guid SongId, string SongTitle, Guid VersionId, IReadOnlyList<ReleaseVersionOptionDto> Versions);

public sealed record ReleaseVersionOptionDto(Guid Id, int Number, string Title, SongVersionStatus Status);

public sealed record CreateReleaseCommand(string Title, ReleaseType Type, DateOnly? ReleaseDate);

public sealed record UpdateReleaseCommand(Guid Id, string Title, ReleaseType Type, DateOnly? ReleaseDate);

/// <summary>La liste complète des pistes, dans l'ordre voulu.</summary>
public sealed record SetReleaseTracksCommand(Guid ReleaseId, IReadOnlyList<ReleaseTrackChoice> Tracks);

public sealed record UploadReleaseCoverCommand(Guid ReleaseId, string FileName, long Length, Stream Content);