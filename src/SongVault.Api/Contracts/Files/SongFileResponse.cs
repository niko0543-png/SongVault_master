using SongVault.Application.Files;
using SongVault.Domain.Files;

namespace SongVault.Api.Contracts.Files;

public sealed record SongFileResponse(Guid Id, string OriginalFileName, string ContentType, long SizeBytes,
    SongFileType FileType, DateTimeOffset UploadedAt)
{
    public static SongFileResponse From(SongFileDto d) =>
        new(d.Id, d.OriginalFileName, d.ContentType, d.SizeBytes, d.FileType, d.UploadedAt);
}