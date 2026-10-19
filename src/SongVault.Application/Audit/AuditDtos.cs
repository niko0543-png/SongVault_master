using SongVault.Domain.Audit;

namespace SongVault.Application.Audit;

/// <summary>
/// Une ligne du journal. ActorId null : SongVault (antivirus) ; ActorEmail null avec un ActorId : compte supprimé.
/// TargetEmail : le compte visé, s'il y en a un et qu'il existe encore.
/// </summary>
public sealed record AuditItemDto(
    long Id, AuditAction Action, string? ActorId, string? ActorEmail,
    string? Subject, string? TargetUserId, string? TargetEmail, string? Details, DateTimeOffset OccurredAt);

/// <summary>Une page du journal ; NextCursor null quand il n'y a plus rien de plus ancien.</summary>
public sealed record AuditPage(IReadOnlyList<AuditItemDto> Items, string? NextCursor);