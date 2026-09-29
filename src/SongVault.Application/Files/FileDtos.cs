using SongVault.Domain.Files;

namespace SongVault.Application.Files;

public sealed record SongFileDto(Guid Id, string OriginalFileName, string ContentType, long SizeBytes,
    SongFileType FileType, DateTimeOffset UploadedAt)
{
    public static SongFileDto From(SongFile f) =>
        new(f.Id, f.OriginalFileName, f.ContentType, f.SizeBytes, f.FileType, f.UploadedAt);
}

/// <summary>Informations internes nécessaires au téléchargement (la clé n'est jamais exposée au client).</summary>
public sealed record StoredFileInfo(string StorageKey, string ContentType, string OriginalFileName);

public sealed record FileDownload(Stream Content, string ContentType, string FileName);

public sealed record UploadSongFileCommand(Guid SongId, Guid VersionId, string FileName, long Length, Stream Content);