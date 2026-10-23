using SongVault.Application.Accounts;
using SongVault.Domain.Accounts;
using System.ComponentModel.DataAnnotations;

namespace SongVault.Api.Contracts.Me;

public sealed record DataExportResponse(
    Guid Id, DataExportStatus Status, DateTimeOffset RequestedAt,
    DateTimeOffset? CompletedAt, DateTimeOffset? ExpiresAt, long? SizeBytes)
{
    public static DataExportResponse From(DataExportDto dto)
        => new(dto.Id, dto.Status, dto.RequestedAt, dto.CompletedAt, dto.ExpiresAt, dto.SizeBytes);

    public sealed record DeleteAccountRequest([Required] string? Password);
}

/// <summary>#26 : « fr » ou « en ».</summary>
public sealed record LocaleRequest([Required] string? Locale);