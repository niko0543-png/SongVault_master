using SongVault.Domain.Files;

namespace SongVault.Application.Files;

/// <summary>Un fichier situé dans son morceau et sa version : contrôle des commentaires et phrase du fil.</summary>
public sealed record SongFileLabel(
    Guid SongId, string SongTitle, Guid VersionId, int VersionNumber, string VersionTitle,
    Guid FileId, string FileName, SongFileType FileType);