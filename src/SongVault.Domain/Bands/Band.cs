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
}