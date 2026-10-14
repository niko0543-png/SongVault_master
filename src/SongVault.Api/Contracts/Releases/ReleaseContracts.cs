using System.ComponentModel.DataAnnotations;

using SongVault.Application.Releases;
using SongVault.Domain.Releases;
using SongVault.Domain.Songs;

namespace SongVault.Api.Contracts.Releases;

/// <summary>Titre, type (Single, EP, Album) et date de sortie, facultative tant qu'elle n'est pas fixée.</summary>
public sealed record SaveReleaseRequest(
    [Required, MaxLength(Release.TitleMaxLength)] string Title,
    [Required] ReleaseType? Type,
    DateOnly? ReleaseDate);

/// <summary>Une piste : le morceau et la version qui figure sur la sortie.</summary>
public sealed record ReleaseTrackRequest([Required] Guid? SongId, [Required] Guid? VersionId);

/// <summary>Toutes les pistes, dans l'ordre : la première de la liste est la piste 1.</summary>
public sealed record SetReleaseTracksRequest([Required, MaxLength(Release.MaxTracks)] IReadOnlyList<ReleaseTrackRequest> Tracks);

public sealed record ReleaseSummaryResponse(
    Guid Id, string Title, ReleaseType Type, DateOnly? ReleaseDate, bool HasCover, int TrackCount)
{
    public static ReleaseSummaryResponse From(ReleaseSummaryDto d)
        => new(d.Id, d.Title, d.Type, d.ReleaseDate, d.HasCover, d.TrackCount);
}

public sealed record ReleaseVersionOptionResponse(Guid Id, int Number, string Title, SongVersionStatus Status);

/// <summary>Une piste ; Versions liste les versions du morceau parmi lesquelles choisir.</summary>
public sealed record ReleaseTrackResponse(
    int TrackNumber, Guid SongId, string SongTitle, Guid VersionId, IReadOnlyList<ReleaseVersionOptionResponse> Versions);

public sealed record ReleaseResponse(
    Guid Id, string Title, ReleaseType Type, DateOnly? ReleaseDate, bool HasCover,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, IReadOnlyList<ReleaseTrackResponse> Tracks)
{
    public static ReleaseResponse From(ReleaseDto d) =>
        new(d.Id, d.Title, d.Type, d.ReleaseDate, d.HasCover, d.CreatedAt, d.UpdatedAt,
            [.. d.Tracks.Select(t => new ReleaseTrackResponse(t.TrackNumber, t.SongId, t.SongTitle, t.VersionId,
                [.. t.Versions.Select(v => new ReleaseVersionOptionResponse(v.Id, v.Number, v.Title, v.Status))]))]);
}