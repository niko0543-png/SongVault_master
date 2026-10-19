using SongVault.Application.Abstractions;
using SongVault.Domain.Audit;
using SongVault.Domain.Bands;

namespace SongVault.Application.Audit;

/// <summary>
/// Écrit le journal d'audit du groupe (#19, ADR 0026), au nom de l'utilisateur de la requête.
/// Comme ActivityLog, n'enregistre rien : la ligne part au SaveChangesAsync du handler, avec l'action,
/// dans la même transaction. Une action refusée ne laisse donc aucune ligne.
/// </summary>
public sealed class AuditLog(IAuditRepository entries, ICurrentUser user)
{
    public void Record(Guid bandId, AuditAction action, string? subject, DateTimeOffset now,
        string? targetUserId = null, string? details = null)
        => entries.Add(AuditEntry.Record(bandId, user.UserId, action, subject, now, targetUserId, details));

    /// <summary>« Member → Owner », en clair dans le journal.</summary>
    public static string RoleChange(BandRole from, BandRole to) => $"{from} → {to}";
}