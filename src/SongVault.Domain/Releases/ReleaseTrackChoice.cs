namespace SongVault.Domain.Releases;

/// <summary>Un morceau et la version choisie pour la sortie. La place vient de l'ordre dans la liste.</summary>
public sealed record ReleaseTrackChoice(Guid SongId, Guid VersionId);