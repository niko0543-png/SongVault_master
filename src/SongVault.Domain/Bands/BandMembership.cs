namespace SongVault.Domain.Bands;

public sealed class BandMembership
{
    public const int UserIdMaxLength = 450;      // même longueur que AspNetUsers.Id

    private BandMembership() { }

    internal BandMembership(Guid bandId, string userId, BandRole role, DateTimeOffset joinedAt)
        => (BandId, UserId, Role, JoinedAt) = (bandId, userId, role, joinedAt);

    public Guid BandId { get; private set; }
    public string UserId { get; private set; } = default!;
    public BandRole Role { get; private set; }
    public DateTimeOffset JoinedAt { get; private set; }

    /// <summary>Appelé uniquement par Band.ChangeRole, qui vérifie la règle du dernier Owner.</summary>
    internal void ChangeRole(BandRole role) => Role = role;
}