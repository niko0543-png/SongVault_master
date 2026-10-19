using SongVault.Application.Abstractions;
using SongVault.Application.Activity;
using SongVault.Application.Common.Exceptions;
using SongVault.Domain.Activity;
using SongVault.Domain.Files;
using SongVault.Application.Billing;
using SongVault.Application.Audit;
using SongVault.Domain.Audit;

namespace SongVault.Application.Files;

public sealed class UploadSongFileHandler(
    ISongRepository songs, IUnitOfWork unitOfWork, IFileStorageService storage, TimeProvider clock,
    ActivityLog activity, AudioPreviewQueue previews, FileScanQueue scans, PlanLimits limits)
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

        await limits.EnsureStorageAsync(song.BandId, command.Length, ct);   // #16 : avant de stocker quoi que ce soit

        var storageKey = $"{Guid.CreateVersion7():N}{rule.Extension}";   // jamais dérivé du nom client
        await storage.SaveAsync(storageKey, command.Content, ct);         // 1. fichier
        SongFileDto saved;
        try
        {
            var now = clock.GetUtcNow();
            var file = version.AddFile(FileNameSanitizer.Sanitize(command.FileName), storageKey,
                rule.ContentType, command.Length, rule.FileType, now);
            activity.FileChanged(ActivityType.FileAdded, song, version, file, now);
            var preview = previews.RequestFor(file, now);                 // #15 : aperçu préparé après l'analyse
            var scan = scans.RequestFor(file, now);                       // #17 : illisible jusqu'au verdict de l'antivirus
            await unitOfWork.SaveChangesAsync(ct);                        // 2. base
            saved = SongFileDto.From(file, preview, scan);
        }
        catch
        {
            await storage.DeleteAsync(storageKey, CancellationToken.None); // compensation
            throw;
        }
        await limits.RefreshStorageAsync(song.BandId, ct);                // 3. jauge (#16), le fichier est enregistré
        return saved;
    }
}

public sealed class DownloadSongFileHandler(ISongRepository songs, IFileStorageService storage)
{
    /// <param name="asAttachment">true : téléchargement sous le nom d'origine ; false : lecture dans le navigateur.</param>
    public async Task<FileDownload> HandleAsync(Guid songId, Guid versionId, Guid fileId, bool asAttachment, CancellationToken ct)
    {
        var info = await songs.GetFileAsync(songId, versionId, fileId, ct)
                   ?? throw new NotFoundException("SongFile", fileId);
        FileScanGate.EnsureReadable(info.Scan);                       // #17 : 409 tant que l'antivirus n'a pas dit « sain »

        // Stockage objet : le navigateur lit le stockage lui-même (ADR 0012)
        var url = storage.CreateDownloadUrl(info.StorageKey, info.ContentType, asAttachment ? info.OriginalFileName : null);
        if (url is not null) return new FileDownload(null, info.ContentType, info.OriginalFileName, url);

        var stream = await storage.OpenReadAsync(info.StorageKey, ct);
        return new FileDownload(stream, info.ContentType, info.OriginalFileName);
    }
}

public sealed class DeleteSongFileHandler(
    ISongRepository songs, IUnitOfWork unitOfWork, IFileStorageService storage, TimeProvider clock,
    ActivityLog activity, PlanLimits limits, AuditLog audit)
{
    public async Task HandleAsync(Guid songId, Guid versionId, Guid fileId, CancellationToken ct)
    {
        var song = await songs.GetWithVersionAsync(songId, versionId, ct) ?? throw new NotFoundException("Song", songId);
        var version = song.FindVersion(versionId) ?? throw new NotFoundException("SongVersion", versionId);
        var file = version.RemoveFile(fileId) ?? throw new NotFoundException("SongFile", fileId);
        var now = clock.GetUtcNow();
        activity.FileChanged(ActivityType.FileRemoved, song, version, file, now);
        audit.Record(song.BandId, AuditAction.FileDeleted, FileSubject(song.Title, version.Number, file.OriginalFileName), now);

        await unitOfWork.SaveChangesAsync(ct);                       // 1. base (l'aperçu part en cascade)
        await limits.RefreshStorageAsync(song.BandId, ct);           //    et la jauge (#16)
        await storage.DeleteAsync(file.StorageKey, CancellationToken.None);  // 2. fichier
        if (file.FileType == SongFileType.Audio)                     // 3. son MP3 d'aperçu (#15), s'il existe
            await storage.DeleteAsync(AudioPreview.StorageKeyFor(file.Id), CancellationToken.None);
    }

    /// <summary>« Nocturne — v2 — mix.wav » : le fichier tel qu'il s'appelait, pour le journal d'audit (#19).</summary>
    public static string FileSubject(string songTitle, int versionNumber, string fileName)
        => $"{songTitle} — v{versionNumber} — {fileName}";
}