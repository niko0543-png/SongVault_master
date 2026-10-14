using SongVault.Domain.Common;

namespace SongVault.Domain.Activity;

/// <summary>Dernière ouverture d'un morceau par un membre : le badge compare les événements à cette date (#11).</summary>
public sealed class SongView
{
    private SongView() { } // réservé à EF Core

    public string UserId { get; private set; } = default!;
    public Guid SongId { get; private set; }
    public DateTimeOffset SeenAt { get; private set; }

    public static SongView Create(string userId, Guid songId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new DomainException("Une ouverture a un utilisateur.");
        if (songId == Guid.Empty) throw new DomainException("Une ouverture porte sur un morceau.");
        return new SongView { UserId = userId, SongId = songId, SeenAt = now };
    }
}