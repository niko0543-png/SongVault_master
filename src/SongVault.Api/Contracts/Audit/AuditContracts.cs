using System.ComponentModel.DataAnnotations;

using SongVault.Application.Audit;
using SongVault.Domain.Audit;

namespace SongVault.Api.Contracts.Audit;

/// <summary>Cursor : NextCursor de la page précédente ; absent pour commencer par le plus récent.</summary>
public sealed class ListAuditRequest
{
    [MaxLength(60)] public string? Cursor { get; init; }
    [Range(1, ListAuditHandler.MaxPageSize)] public int PageSize { get; init; } = ListAuditHandler.DefaultPageSize;
}

/// <summary>
/// Une ligne du journal. ActorId null : SongVault (antivirus) ; ActorEmail null avec un ActorId : compte supprimé.
/// TargetEmail null avec un TargetUserId : compte supprimé.
/// </summary>
public sealed record AuditItemResponse(
    long Id, AuditAction Action, string? ActorId, string? ActorEmail,
    string? Subject, string? TargetUserId, string? TargetEmail, string? Details, DateTimeOffset OccurredAt)
{
    public static AuditItemResponse From(AuditItemDto d) =>
        new(d.Id, d.Action, d.ActorId, d.ActorEmail, d.Subject, d.TargetUserId, d.TargetEmail, d.Details, d.OccurredAt);
}

/// <summary>NextCursor null : c'était la dernière page.</summary>
public sealed record AuditPageResponse(IReadOnlyList<AuditItemResponse> Items, string? NextCursor);