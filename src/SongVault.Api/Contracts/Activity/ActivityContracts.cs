using System.ComponentModel.DataAnnotations;

using SongVault.Application.Activity;
using SongVault.Domain.Activity;

namespace SongVault.Api.Contracts.Activity;

/// <summary>Cursor : NextCursor de la page précédente ; absent pour commencer par le plus récent.</summary>
public sealed class ListActivityRequest
{
    [MaxLength(60)] public string? Cursor { get; init; }
    [Range(1, ListActivityHandler.MaxPageSize)] public int PageSize { get; init; } = ListActivityHandler.DefaultPageSize;
}

/// <summary>
/// Une ligne du fil. ActorEmail null : compte supprimé. SongExists false : le morceau a été supprimé depuis,
/// SongTitle garde son titre d'alors. EntityId : le fichier ou le commentaire concerné.
/// </summary>
public sealed record ActivityItemResponse(
    long Id, ActivityType Type, string? ActorId, string? ActorEmail,
    Guid SongId, bool SongExists, Guid? VersionId, Guid? EntityId,
    string SongTitle, int? VersionNumber, string? VersionTitle, string? FileName,
    DateTimeOffset CreatedAt)
{
    public static ActivityItemResponse From(ActivityItemDto d) =>
        new(d.Id, d.Type, d.ActorId, d.ActorEmail, d.SongId, d.SongExists, d.VersionId, d.EntityId,
            d.SongTitle, d.VersionNumber, d.VersionTitle, d.FileName, d.CreatedAt);
}

/// <summary>NextCursor null : c'était la dernière page.</summary>
public sealed record ActivityPageResponse(IReadOnlyList<ActivityItemResponse> Items, string? NextCursor);