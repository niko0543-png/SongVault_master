namespace SongVault.Domain.Invitations;

/// <summary>Invitation déjà utilisée, annulée ou expirée. Traduite en HTTP 410.</summary>
public sealed class InvitationUnavailableException(InvitationStatus status) : Exception(status switch
{
    InvitationStatus.Accepted => "Cette invitation a déjà été utilisée.",
    InvitationStatus.Revoked => "Cette invitation a été annulée.",
    _ => "Cette invitation a expiré.",
})
{
    public InvitationStatus Status { get; } = status;
}