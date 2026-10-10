using System.ComponentModel.DataAnnotations;

using SongVault.Application.Bands;
using SongVault.Domain.Bands;

namespace SongVault.Api.Contracts.Bands;

public sealed record BandMemberResponse(string UserId, string Email, BandRole Role, DateTimeOffset JoinedAt)
{
    public static BandMemberResponse From(BandMemberDto dto) => new(dto.UserId, dto.Email, dto.Role, dto.JoinedAt);
}

/// <summary>Nouveau rôle. Nullable + [Required] : un corps sans « role » donne 400 au lieu de Owner (valeur 0).</summary>
public sealed record ChangeMemberRoleRequest([Required] BandRole? Role);