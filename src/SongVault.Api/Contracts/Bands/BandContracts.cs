using System.ComponentModel.DataAnnotations;

using SongVault.Application.Bands;
using SongVault.Domain.Bands;

namespace SongVault.Api.Contracts.Bands;

public sealed record BandResponse(Guid Id, string Name, BandRole Role)
{
    public static BandResponse From(BandSummaryDto dto) => new(dto.Id, dto.Name, dto.Role);
}

public sealed record CreateBandRequest([Required, MaxLength(Band.NameMaxLength)] string Name);

public sealed record RenameBandRequest([Required, MaxLength(Band.NameMaxLength)] string Name);