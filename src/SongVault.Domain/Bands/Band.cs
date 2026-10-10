using SongVault.Domain.Common;

namespace SongVault.Domain.Bands;

public sealed class Band
{
    public const int NameMaxLength = 100;

    private readonly List<BandMembership> _memberships = [];
    private Band() { } // réservé à EF Core

    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public DateTimeOffset CreatedAt { get; private set; }
    public IReadOnlyCollection<BandMembership> Memberships => _memberships.AsReadOnly();

    public static Band Create(string name, string ownerUserId, DateTimeOffset now)
    {
        var cleanName = Text.Required(name, NameMaxLength, "nom du groupe");
        if (string.IsNullOrWhiteSpace(ownerUserId))
            throw new DomainException("Un groupe doit avoir un propriétaire.");

        var band = new Band { Id = Guid.CreateVersion7(now), Name = cleanName, CreatedAt = now };
        band._memberships.Add(new BandMembership(band.Id, ownerUserId, BandRole.Owner, now));
        return band;
    }

    public void Rename(string name) => Name = Text.Required(name, NameMaxLength, "nom du groupe");

    public bool HasMember(string userId) => _memberships.Any(m => m.UserId == userId);

    /// <summary>Ajoute un membre. Servira aux invitations (#3) ; en #2, seuls les tests l'appellent.</summary>
    public void AddMember(string userId, BandRole role, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new DomainException("Un membre doit avoir un identifiant.");
        if (HasMember(userId))
            throw new DomainException("Cet utilisateur est déjà membre du groupe.");

        _memberships.Add(new BandMembership(Id, userId, role, now));
    }

    /// <summary>Change le rôle d'un membre. Le dernier Owner ne peut pas perdre ce rôle.</summary>
    public void ChangeRole(string userId, BandRole role)
    {
        var membership = Find(userId);
        if (membership.Role == BandRole.Owner && role != BandRole.Owner)
            EnsureAnotherOwner(userId);

        membership.ChangeRole(role);
    }

    /// <summary>Retire un membre (exclusion par un Owner ou départ volontaire). Le dernier Owner ne peut pas partir.</summary>
    public void RemoveMember(string userId)
    {
        var membership = Find(userId);
        if (membership.Role == BandRole.Owner)
            EnsureAnotherOwner(userId);

        _memberships.Remove(membership);   // relation obligatoire : EF supprime la ligne orpheline
    }

    private BandMembership Find(string userId)
        => _memberships.SingleOrDefault(m => m.UserId == userId)
           ?? throw new DomainException("Cet utilisateur n'est pas membre du groupe.");

    private void EnsureAnotherOwner(string userId)
    {
        if (!_memberships.Any(m => m.Role == BandRole.Owner && m.UserId != userId))
            throw new LastOwnerException();
    }
}