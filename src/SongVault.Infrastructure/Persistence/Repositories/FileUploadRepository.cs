using Microsoft.EntityFrameworkCore;

using SongVault.Application.Abstractions;
using SongVault.Domain.Files;

namespace SongVault.Infrastructure.Persistence.Repositories;

internal sealed class FileUploadRepository(SongVaultDbContext db) : IFileUploadRepository
{
    public void Add(FileUpload upload) => db.FileUploads.Add(upload);

    public Task<FileUpload?> GetAsync(Guid uploadId, CancellationToken ct)
        => db.FileUploads.FirstOrDefaultAsync(u => u.Id == uploadId, ct);

    public void Remove(FileUpload upload) => db.FileUploads.Remove(upload);
}