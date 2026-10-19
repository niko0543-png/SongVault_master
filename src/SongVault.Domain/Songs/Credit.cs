namespace SongVault.Domain.Songs;

/// <summary>
/// Un ayant droit d'un morceau (#22) : nom, rôle et part des droits. Les crédits d'un morceau se remplacent
/// ensemble (Song.SetRights), pour que la règle des 100 % porte toujours sur l'ensemble.
/// </summary>
public sealed class Credit
{
    public const int NameMaxLength = 200;

    private Credit() { } // réservé à EF Core

    public Guid Id { get; private set; }
    public Guid SongId { get; private set; }
    /// <summary>Ordre d'affichage, à partir de 1 : celui de la liste envoyée.</summary>
    public int Position { get; private set; }
    public string Name { get; private set; } = default!;
    public CreditRole Role { get; private set; }
    /// <summary>Part des droits, de 0,01 à 100, deux décimales au plus (decimal(5,2) en base).</summary>
    public decimal SharePercent { get; private set; }

    internal static Credit Create(Guid songId, int position, string name, CreditRole role, decimal sharePercent)
        => new() { Id = Guid.CreateVersion7(), SongId = songId, Position = position, Name = name, Role = role, SharePercent = sharePercent };
}