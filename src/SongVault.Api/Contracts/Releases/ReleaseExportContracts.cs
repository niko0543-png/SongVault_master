using SongVault.Application.Releases.Exports;
using SongVault.Domain.Releases;

namespace SongVault.Api.Contracts.Releases;

/// <summary>Archive ZIP d'une sortie : en préparation, prête (taille, date d'expiration) ou en échec.</summary>
public sealed record ReleaseExportResponse(
    Guid Id, ReleaseExportStatus Status, DateTimeOffset RequestedAt,
    DateTimeOffset? CompletedAt, DateTimeOffset? ExpiresAt, long? SizeBytes)
{
    public static ReleaseExportResponse From(ReleaseExportDto dto)
        => new(dto.Id, dto.Status, dto.RequestedAt, dto.CompletedAt, dto.ExpiresAt, dto.SizeBytes);
}