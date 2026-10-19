using SongVault.Application.Abstractions;
using SongVault.Application.Common.Exceptions;
using SongVault.Application.Files;
using SongVault.Domain.Common;
using SongVault.Domain.Releases;
using SongVault.Application.Audit;
using SongVault.Domain.Audit;

namespace SongVault.Application.Releases;

// Lire = tout membre ; écrire = Member ou Owner (ADR 0008, appliqué par [BandScoped] sur le contrôleur).

public sealed class ListReleasesHandler(IReleaseRepository releases)
{
    public Task<IReadOnlyList<ReleaseSummaryDto>> HandleAsync(CancellationToken ct) => releases.ListAsync(ct);
}

public sealed class GetReleaseHandler(IReleaseRepository releases)
{
    public async Task<ReleaseDto> HandleAsync(Guid id, CancellationToken ct)
        => await releases.GetDetailAsync(id, ct) ?? throw new NotFoundException("Release", id);
}

public sealed class CreateReleaseHandler(
    IReleaseRepository releases, IUnitOfWork unitOfWork, IBandContext band, TimeProvider clock)
{
    public async Task<ReleaseDto> HandleAsync(CreateReleaseCommand command, CancellationToken ct)
    {
        var release = Release.Create(band.RequiredBandId, command.Title, command.Type, command.ReleaseDate, clock.GetUtcNow());
        releases.Add(release);
        await unitOfWork.SaveChangesAsync(ct);
        return ReleaseDto.FromNew(release);
    }
}

public sealed class UpdateReleaseHandler(IReleaseRepository releases, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public async Task HandleAsync(UpdateReleaseCommand command, CancellationToken ct)
    {
        var release = await releases.GetRequiredAsync(command.Id, ct);
        release.UpdateDetails(command.Title, command.Type, command.ReleaseDate, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(ct);
    }
}

public sealed class DeleteReleaseHandler(
    IReleaseRepository releases, IUnitOfWork unitOfWork, IFileStorageService storage, AuditLog audit, TimeProvider clock)
{
    /// <summary>Supprime la sortie et ses pistes. Les morceaux et leurs versions restent.</summary>
    public async Task HandleAsync(Guid id, CancellationToken ct)
    {
        var release = await releases.GetRequiredAsync(id, ct);
        var cover = release.CoverStorageKey;

        releases.Remove(release);
        audit.Record(release.BandId, AuditAction.ReleaseDeleted, release.Title, clock.GetUtcNow());   // #19
        await unitOfWork.SaveChangesAsync(ct);                        // 1. base (pistes en cascade)
        if (cover is not null)
            await storage.DeleteAsync(cover, CancellationToken.None); // 2. pochette
    }
}

public sealed class SetReleaseTracksHandler(
    IReleaseRepository releases, ISongRepository songs, IUnitOfWork unitOfWork, TimeProvider clock)
{
    /// <summary>
    /// Remplace les pistes par la liste reçue, dans son ordre. Chaque version doit appartenir à son morceau,
    /// et chaque morceau au groupe de la sortie.
    /// </summary>
    public async Task<ReleaseDto> HandleAsync(SetReleaseTracksCommand command, CancellationToken ct)
    {
        var release = await releases.GetRequiredAsync(command.ReleaseId, ct);
        Release.EnsureValidTracks(command.Tracks);

        // Lecture limitée au groupe actif : un morceau d'un autre groupe n'y figure pas
        var songOfVersion = await songs.GetVersionSongIdsAsync([.. command.Tracks.Select(t => t.VersionId)], ct);
        foreach (var track in command.Tracks)
            if (!songOfVersion.TryGetValue(track.VersionId, out var songId) || songId != track.SongId)
                throw new DomainException("Chaque version doit appartenir à son morceau, dans le groupe de la sortie.");

        // Deux passes dans une transaction : l'index unique (ReleaseId, TrackNumber) est vérifié ligne à ligne,
        // un échange 1 ↔ 2 en une seule passe le violerait.
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            release.ParkTrackNumbers();
            await unitOfWork.SaveChangesAsync(token);
            release.SetTracks(command.Tracks, clock.GetUtcNow());
            await unitOfWork.SaveChangesAsync(token);
        }, ct);

        return await releases.GetDetailAsync(release.Id, ct) ?? throw new NotFoundException("Release", release.Id);
    }
}

public sealed class UploadReleaseCoverHandler(
    IReleaseRepository releases, IUnitOfWork unitOfWork, IFileStorageService storage, TimeProvider clock)
{
    public async Task HandleAsync(UploadReleaseCoverCommand command, CancellationToken ct)
    {
        var rule = CoverImagePolicy.Resolve(command.FileName)
                   ?? throw new UnsupportedFileException(
                       $"Type d'image non autorisé. Extensions acceptées : {string.Join(", ", CoverImagePolicy.AllowedExtensions)}.");
        if (command.Length <= 0) throw new UnsupportedFileException("Le fichier est vide.");
        if (command.Length > CoverImagePolicy.MaxFileSizeBytes) throw new FileTooLargeException(CoverImagePolicy.MaxFileSizeBytes);

        if (!command.Content.CanSeek)
            throw new InvalidOperationException("Le flux d'upload doit permettre le repositionnement.");
        var header = new byte[FileSignatureValidator.HeaderLength];
        var read = await command.Content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
        if (!FileSignatureValidator.Matches(rule.Extension, header.AsSpan(0, read)))
            throw new UnsupportedFileException($"Le contenu du fichier ne correspond pas à une image {rule.Extension}.");
        command.Content.Position = 0;

        var release = await releases.GetRequiredAsync(command.ReleaseId, ct);

        var storageKey = $"{Guid.CreateVersion7():N}{rule.Extension.ToLowerInvariant()}";
        await storage.SaveAsync(storageKey, command.Content, ct);         // 1. fichier
        string? previous;
        try
        {
            previous = release.SetCover(storageKey, rule.ContentType, clock.GetUtcNow());
            await unitOfWork.SaveChangesAsync(ct);                        // 2. base
        }
        catch
        {
            await storage.DeleteAsync(storageKey, CancellationToken.None); // compensation
            throw;
        }
        if (previous is not null)
            await storage.DeleteAsync(previous, CancellationToken.None);  // 3. ancienne pochette
    }
}

public sealed class DownloadReleaseCoverHandler(IReleaseRepository releases, IFileStorageService storage)
{
    public async Task<FileDownload> HandleAsync(Guid id, CancellationToken ct)
    {
        var release = await releases.GetRequiredAsync(id, ct);
        if (release.CoverStorageKey is not { } key || release.CoverContentType is not { } contentType)
            throw new NotFoundException("ReleaseCover", id);

        var fileName = $"pochette{Path.GetExtension(key)}";
        var url = storage.CreateDownloadUrl(key, contentType, null);
        if (url is not null) return new FileDownload(null, contentType, fileName, url);

        return new FileDownload(await storage.OpenReadAsync(key, ct), contentType, fileName);
    }
}

public sealed class DeleteReleaseCoverHandler(
    IReleaseRepository releases, IUnitOfWork unitOfWork, IFileStorageService storage, TimeProvider clock)
{
    public async Task HandleAsync(Guid id, CancellationToken ct)
    {
        var release = await releases.GetRequiredAsync(id, ct);
        var previous = release.RemoveCover(clock.GetUtcNow());
        if (previous is null) return;

        await unitOfWork.SaveChangesAsync(ct);
        await storage.DeleteAsync(previous, CancellationToken.None);
    }
}

internal static class ReleaseLookups
{
    public static async Task<Release> GetRequiredAsync(this IReleaseRepository releases, Guid id, CancellationToken ct)
        => await releases.GetAsync(id, ct) ?? throw new NotFoundException("Release", id);
}