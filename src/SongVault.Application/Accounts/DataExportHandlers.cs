using SongVault.Application.Abstractions;
using SongVault.Application.Common.Exceptions;
using SongVault.Application.Files;
using SongVault.Domain.Accounts;

namespace SongVault.Application.Accounts;

public sealed record DataExportDto(
    Guid Id, DataExportStatus Status, DateTimeOffset RequestedAt,
    DateTimeOffset? CompletedAt, DateTimeOffset? ExpiresAt, long? SizeBytes)
{
    public static DataExportDto From(DataExport e)
        => new(e.Id, e.Status, e.RequestedAt, e.CompletedAt, e.ExpiresAt, e.SizeBytes);
}

public sealed class RequestDataExportHandler(
    IAccountRepository accounts, IBackgroundJobs jobs, IUnitOfWork unitOfWork, ICurrentUser currentUser,
    TimeProvider clock)
{
    /// <summary>Demande une archive. Refusé (409) si la précédente est encore en préparation.</summary>
    public async Task<DataExportDto> HandleAsync(CancellationToken ct)
    {
        var latest = await accounts.GetLatestExportAsync(currentUser.UserId, ct);
        if (latest is { Status: DataExportStatus.Pending })
            throw new ConflictException("Une archive est déjà en préparation : un e-mail vous préviendra quand elle sera prête.");

        var now = clock.GetUtcNow();
        var export = DataExport.Request(currentUser.UserId, now);
        accounts.AddExport(export);
        jobs.Enqueue(ExportAccountJob.JobType, export.Id.ToString(), now);
        await unitOfWork.SaveChangesAsync(ct);                  // l'export et sa tâche, ensemble
        return DataExportDto.From(export);
    }
}

public sealed class GetLatestDataExportHandler(IAccountRepository accounts, ICurrentUser currentUser, TimeProvider clock)
{
    /// <summary>Dernier export du compte ; null s'il n'y en a pas, ou s'il a expiré.</summary>
    public async Task<DataExportDto?> HandleAsync(CancellationToken ct)
    {
        var export = await accounts.GetLatestExportAsync(currentUser.UserId, ct);
        if (export is null) return null;
        if (export.Status == DataExportStatus.Ready && !export.IsDownloadableAt(clock.GetUtcNow())) return null;
        return DataExportDto.From(export);
    }
}

public sealed class DownloadDataExportHandler(
    IAccountRepository accounts, IFileStorageService storage, ICurrentUser currentUser, TimeProvider clock)
{
    /// <summary>Archive du dernier export, s'il est prêt et pas expiré (sinon 404).</summary>
    public async Task<FileDownload> HandleAsync(CancellationToken ct)
    {
        var export = await accounts.GetLatestExportAsync(currentUser.UserId, ct);
        if (export is null || !export.IsDownloadableAt(clock.GetUtcNow()))
            throw new NotFoundException("DataExport", currentUser.UserId);

        var fileName = $"songvault-export-{export.CompletedAt:yyyy-MM-dd}.zip";
        var url = storage.CreateDownloadUrl(export.StorageKey!, "application/zip", fileName);
        if (url is not null) return new FileDownload(null, "application/zip", fileName, url);

        var content = await storage.OpenReadAsync(export.StorageKey!, ct);
        return new FileDownload(content, "application/zip", fileName);
    }
}