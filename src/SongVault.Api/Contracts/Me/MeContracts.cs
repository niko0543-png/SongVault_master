using SongVault.Application.Accounts;
using SongVault.Domain.Accounts;

namespace SongVault.Api.Contracts.Me;

public sealed record DataExportResponse(
    Guid Id, DataExportStatus Status, DateTimeOffset RequestedAt,
    DateTimeOffset? CompletedAt, DateTimeOffset? ExpiresAt, long? SizeBytes)
{
    public static DataExportResponse From(DataExportDto dto)
        => new(dto.Id, dto.Status, dto.RequestedAt, dto.CompletedAt, dto.ExpiresAt, dto.SizeBytes);
}