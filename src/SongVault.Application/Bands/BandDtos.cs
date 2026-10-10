using SongVault.Domain.Bands;

namespace SongVault.Application.Bands;

/// <summary>Un groupe tel que le voit l'utilisateur courant : identifiant, nom et son rôle dans ce groupe.</summary>
public sealed record BandSummaryDto(Guid Id, string Name, BandRole Role);

/// <summary>Un membre du groupe : identifiant Identity, e-mail (affiché dans la liste), rôle et date d'arrivée.</summary>
public sealed record BandMemberDto(string UserId, string Email, BandRole Role, DateTimeOffset JoinedAt);