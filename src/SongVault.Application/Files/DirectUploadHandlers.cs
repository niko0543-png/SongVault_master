using SongVault.Application.Abstractions;
using SongVault.Application.Activity;
using SongVault.Application.Common.Exceptions;
using SongVault.Domain.Activity;
using SongVault.Domain.Files;

namespace SongVault.Application.Files;

/// <summary>1. Annonce : contrôles, puis adresse où le navigateur envoie le fichier (ADR 0012).</summary>
public sealed class StartFileUploadHandler(
    ISongRepository songs, IFileUploadRepository uploads, IFileStorageService storage, IBackgroundJobs jobs,
    IUnitOfWork unitOfWork, ICurrentUser currentUser, TimeProvider clock)
{
    public async Task<FileUploadTicket> HandleAsync(StartFileUploadCommand command, CancellationToken ct)
    {
        var rule = FileTypePolicy.Resolve(command.FileName)
                   ?? throw new UnsupportedFileException(
                       $"Type de fichier non autorisé. Extensions acceptées : {string.Join(", ", FileTypePolicy.AllowedExtensions)}.");
        if (command.SizeBytes <= 0) throw new UnsupportedFileException("Le fichier est vide.");
        if (command.SizeBytes > FileTypePolicy.MaxDirectUploadBytes)
            throw new FileTooLargeException(FileTypePolicy.MaxDirectUploadBytes);

        var song = await songs.GetWithVersionAsync(command.SongId, command.VersionId, ct)
                   ?? throw new NotFoundException("Song", command.SongId);
        _ = song.FindVersion(command.VersionId) ?? throw new NotFoundException("SongVersion", command.VersionId);

        var upload = FileUpload.Start(command.SongId, command.VersionId, currentUser.UserId,
            FileNameSanitizer.Sanitize(command.FileName),
            $"{Guid.CreateVersion7():N}{rule.Extension}",                  // jamais dérivé du nom client
            command.SizeBytes, clock.GetUtcNow());
        uploads.Add(upload);
        jobs.Enqueue(ExpireFileUploadJob.JobType, upload.Id.ToString(), upload.ExpiresAt);
        await unitOfWork.SaveChangesAsync(ct);                              // l'envoi et son nettoyage, ensemble

        return new FileUploadTicket(upload.Id, storage.CreateUploadUrl(upload.StorageKey, upload.ExpiresAt), upload.ExpiresAt);
    }
}

/// <summary>2. Stockage local seulement : l'API reçoit le PUT du navigateur à la place du stockage objet.</summary>
public sealed class ReceiveFileUploadHandler(
    IFileUploadRepository uploads, IFileStorageService storage, ICurrentUser currentUser, TimeProvider clock)
{
    public async Task HandleAsync(Guid songId, Guid versionId, Guid uploadId, long? contentLength, Stream content,
        CancellationToken ct)
    {
        var upload = await uploads.GetAsync(uploadId, ct);
        if (upload is null || !upload.BelongsTo(songId, versionId, currentUser.UserId) || upload.IsExpiredAt(clock.GetUtcNow()))
            throw new NotFoundException("FileUpload", uploadId);
        if (contentLength != upload.SizeBytes)
            throw new UnsupportedFileException("La taille envoyée ne correspond pas à la taille annoncée.");
        if (await storage.GetLengthAsync(upload.StorageKey, ct) is not null)
            throw new ConflictException("Ce fichier a déjà été envoyé.");

        await storage.SaveAsync(upload.StorageKey, content, ct);
    }
}

/// <summary>3. Confirmation : taille et signature contrôlées, puis fichier rattaché à la version.</summary>
public sealed class CompleteFileUploadHandler(
    ISongRepository songs, IFileUploadRepository uploads, IFileStorageService storage, IUnitOfWork unitOfWork,
    ICurrentUser currentUser, TimeProvider clock, ActivityLog activity)
{
    public async Task<SongFileDto> HandleAsync(Guid songId, Guid versionId, Guid uploadId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var upload = await uploads.GetAsync(uploadId, ct);
        if (upload is null || !upload.BelongsTo(songId, versionId, currentUser.UserId) || upload.IsExpiredAt(now))
            throw new NotFoundException("FileUpload", uploadId);

        var song = await songs.GetWithVersionAsync(songId, versionId, ct) ?? throw new NotFoundException("Song", songId);
        var version = song.FindVersion(versionId) ?? throw new NotFoundException("SongVersion", versionId);

        var length = await storage.GetLengthAsync(upload.StorageKey, ct)
                     ?? throw new ConflictException("Le fichier n'est pas encore arrivé : envoyez-le avant de confirmer.");
        var rule = FileTypePolicy.Resolve(upload.StorageKey)!;              // la clé porte l'extension vérifiée à l'annonce

        // Une URL signée ne limite pas ce qui est envoyé : on vérifie ce qui est réellement arrivé
        var problem = length != upload.SizeBytes
            ? "La taille reçue ne correspond pas à la taille annoncée."
            : !await HasValidSignatureAsync(upload.StorageKey, rule.Extension, ct)
                ? $"Le contenu du fichier ne correspond pas à un fichier {rule.Extension}."
                : null;
        if (problem is not null)
        {
            await storage.DeleteAsync(upload.StorageKey, CancellationToken.None);
            uploads.Remove(upload);
            await unitOfWork.SaveChangesAsync(ct);
            throw new UnsupportedFileException(problem);
        }

        var file = version.AddFile(upload.FileName, upload.StorageKey, rule.ContentType, length, rule.FileType, now);
        uploads.Remove(upload);
        activity.FileChanged(ActivityType.FileAdded, song, version, file, now);
        // Le fichier rattaché et l'envoi effacé, ensemble. Si la tâche d'expiration vient d'effacer la ligne,
        // l'effacement ne touche aucune ligne : EF lève un conflit (409) et rien n'est rattaché.
        await unitOfWork.SaveChangesAsync(ct);
        return SongFileDto.From(file);
    }

    private async Task<bool> HasValidSignatureAsync(string storageKey, string extension, CancellationToken ct)
    {
        await using var content = await storage.OpenReadAsync(storageKey, ct);
        var header = new byte[FileSignatureValidator.HeaderLength];
        var read = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
        return FileSignatureValidator.Matches(extension, header.AsSpan(0, read));
    }
}

/// <summary>Efface un envoi jamais confirmé, et ce qui a pu être envoyé (payload : identifiant de l'envoi).</summary>
public sealed class ExpireFileUploadJob(IFileUploadRepository uploads, IFileStorageService storage, IUnitOfWork unitOfWork)
    : IBackgroundJobHandler
{
    public const string JobType = "file-upload.expire";

    public string Type => JobType;

    public async Task RunAsync(BackgroundJobContext job, CancellationToken ct)
    {
        var upload = await uploads.GetAsync(Guid.Parse(job.Payload), ct);
        if (upload is null) return;                                 // confirmé, ou refusé à la confirmation

        uploads.Remove(upload);
        await unitOfWork.SaveChangesAsync(ct);                      // la ligne d'abord : une confirmation en cours échoue
        await storage.DeleteAsync(upload.StorageKey, CancellationToken.None);
    }
}