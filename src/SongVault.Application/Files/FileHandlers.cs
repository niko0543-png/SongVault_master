using SongVault.Application.Abstractions;
using SongVault.Application.Common.Exceptions;

namespace SongVault.Application.Files;

public sealed class UploadSongFileHandler(
    ISongRepository songs, IUnitOfWork unitOfWork, IFileStorageService storage, TimeProvider clock)
{
    public async Task<SongFileDto> HandleAsync(UploadSongFileCommand command, CancellationToken ct)
    {
        var rule = FileTypePolicy.Resolve(command.FileName)
        ?? throw new UnsupportedFileException(
            $"Type de fichier non autorisé. Extensions acceptées : {string.Join(", ", FileTypePolicy.AllowedExtensions)}.");
        if (command.Length <= 0) throw new UnsupportedFileException("Le fichier est vide.");
        if (command.Length > FileTypePolicy.MaxFileSizeBytes) throw new FileTooLargeException(FileTypePolicy.MaxFileSizeBytes);

        // NOUVEAU : vérification du contenu (32 premiers octets seulement)
        if (!command.Content.CanSeek)
            throw new InvalidOperationException("Le flux d'upload doit permettre le repositionnement.");
        var header = new byte[FileSignatureValidator.HeaderLength];
        var read = await command.Content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
        if (!FileSignatureValidator.Matches(rule.Extension, header.AsSpan(0, read)))
            throw new UnsupportedFileException($"Le contenu du fichier ne correspond pas à un fichier {rule.Extension}.");
        command.Content.Position = 0;                                   // sinon l'en-tête manquerait au fichier stocké

        var song = await songs.GetWithVersionAsync(command.SongId, command.VersionId, ct)
                   ?? throw new NotFoundException("Song", command.SongId);
        var version = song.FindVersion(command.VersionId)
                      ?? throw new NotFoundException("SongVersion", command.VersionId);

        var storageKey = $"{Guid.CreateVersion7():N}{rule.Extension}";   // jamais dérivé du nom client
        await storage.SaveAsync(storageKey, command.Content, ct);         // 1. fichier
        try
        {
            var file = version.AddFile(FileNameSanitizer.Sanitize(command.FileName), storageKey,
                rule.ContentType, command.Length, rule.FileType, clock.GetUtcNow());
            await unitOfWork.SaveChangesAsync(ct);                        // 2. base
            return SongFileDto.From(file);
        }
        catch
        {
            await storage.DeleteAsync(storageKey, CancellationToken.None); // compensation
            throw;
        }
    }
}

public sealed class DownloadSongFileHandler(ISongRepository songs, IFileStorageService storage)
{
    public async Task<FileDownload> HandleAsync(Guid songId, Guid versionId, Guid fileId, CancellationToken ct)
    {
        var info = await songs.GetFileAsync(songId, versionId, fileId, ct)
                   ?? throw new NotFoundException("SongFile", fileId);
        var stream = await storage.OpenReadAsync(info.StorageKey, ct);
        return new FileDownload(stream, info.ContentType, info.OriginalFileName);
    }
}

public sealed class DeleteSongFileHandler(ISongRepository songs, IUnitOfWork unitOfWork, IFileStorageService storage)
{
    public async Task HandleAsync(Guid songId, Guid versionId, Guid fileId, CancellationToken ct)
    {
        var song = await songs.GetWithVersionAsync(songId, versionId, ct) ?? throw new NotFoundException("Song", songId);
        var version = song.FindVersion(versionId) ?? throw new NotFoundException("SongVersion", versionId);
        var file = version.RemoveFile(fileId) ?? throw new NotFoundException("SongFile", fileId);

        await unitOfWork.SaveChangesAsync(ct);                       // 1. base
        await storage.DeleteAsync(file.StorageKey, CancellationToken.None);  // 2. fichier
    }
}