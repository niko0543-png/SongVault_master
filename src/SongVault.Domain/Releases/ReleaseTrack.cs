namespace SongVault.Domain.Releases;

/// <summary>
/// Une piste d'une sortie : quel morceau, dans quelle version, à quelle place.
/// Clé (ReleaseId, SongId) : un morceau figure au plus une fois sur une sortie.
/// </summary>
public sealed class ReleaseTrack
{
    private ReleaseTrack() { } // réservé à EF Core

    public Guid ReleaseId { get; private set; }
    public Guid SongId { get; private set; }
    public Guid VersionId { get; private set; }
    /// <summary>Place sur la sortie, à partir de 1. Négative le temps d'un réordonnancement (voir Release.ParkTrackNumbers).</summary>
    public int TrackNumber { get; private set; }

    internal static ReleaseTrack Create(Guid releaseId, Guid songId, Guid versionId, int trackNumber)
        => new() { ReleaseId = releaseId, SongId = songId, VersionId = versionId, TrackNumber = trackNumber };

    internal void Place(int trackNumber, Guid versionId)
    {
        TrackNumber = trackNumber;
        VersionId = versionId;
    }
}