using SongVault.Domain.Common;

namespace SongVault.Domain.Activity;

/// <summary>
/// De quoi écrire la phrase du fil (« Ana a ajouté la v4 de Nocturne ») sans relire le morceau,
/// même après sa suppression. Stocké en JSON dans la colonne ActivityEvents.Details.
/// </summary>
public sealed class ActivityDetails
{
    private ActivityDetails() { } // réservé à EF Core

    public ActivityDetails(string songTitle, int? versionNumber = null, string? versionTitle = null, string? fileName = null)
    {
        if (string.IsNullOrWhiteSpace(songTitle))
            throw new DomainException("Un événement nomme le morceau concerné.");
        SongTitle = songTitle;
        VersionNumber = versionNumber;
        VersionTitle = versionTitle;
        FileName = fileName;
    }

    /// <summary>Titre du morceau au moment de l'événement.</summary>
    public string SongTitle { get; private set; } = default!;
    public int? VersionNumber { get; private set; }
    public string? VersionTitle { get; private set; }
    public string? FileName { get; private set; }
}