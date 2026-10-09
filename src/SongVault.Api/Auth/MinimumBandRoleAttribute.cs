using SongVault.Domain.Bands;

namespace SongVault.Api.Auth;

/// <summary>
/// Rôle minimal exigé dans le groupe pour cette action (ou tout le contrôleur), lu par BandScopedFilter.
/// Sans cet attribut : Guest pour GET et HEAD, Member pour les autres méthodes (ADR 0008).
/// N'a d'effet que sur une action qui porte aussi [BandScoped].
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class MinimumBandRoleAttribute(BandRole role) : Attribute
{
    public BandRole Role { get; } = role;
}