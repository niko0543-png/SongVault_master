using SongVault.Application.Invitations;
using SongVault.Domain.Invitations;

namespace SongVault.Application.Abstractions;

/// <summary>Accès aux invitations. Un seul dépôt pour l'agrégat Invitation (ADR 0006).</summary>
public interface IInvitationRepository
{
    /// <summary>Ajoute une invitation neuve au contexte. N'écrit rien : l'appelant fait SaveChangesAsync.</summary>
    void Add(Invitation invitation);

    /// <summary>
    /// Invitation suivie par EF dont l'empreinte de jeton est celle-ci, ou null.
    /// Utilisé par PreviewInvitationHandler et AcceptInvitationHandler.
    /// </summary>
    Task<Invitation?> GetByTokenHashAsync(byte[] tokenHash, CancellationToken ct);

    /// <summary>Invitation suivie par EF, ou null si elle n'existe pas dans ce groupe. Utilisé par RevokeInvitationHandler.</summary>
    Task<Invitation?> GetInBandAsync(Guid bandId, Guid invitationId, CancellationToken ct);

    /// <summary>
    /// Invitations encore en attente pour cette adresse dans le groupe, suivies par EF.
    /// CreateInvitationHandler les annule avant d'en créer une nouvelle.
    /// </summary>
    Task<IReadOnlyList<Invitation>> ListPendingForEmailAsync(Guid bandId, string email, DateTimeOffset now, CancellationToken ct);

    /// <summary>
    /// Invitations en attente du groupe, les plus récentes d'abord. Lecture sans suivi.
    /// Utilisé par GET /api/bands/{bandId}/invitations (ListInvitationsHandler).
    /// </summary>
    Task<IReadOnlyList<InvitationDto>> ListPendingAsync(Guid bandId, DateTimeOffset now, CancellationToken ct);
}