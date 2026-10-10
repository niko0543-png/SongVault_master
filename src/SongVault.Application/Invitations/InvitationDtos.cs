using SongVault.Domain.Bands;

namespace SongVault.Application.Invitations;

/// <summary>Invitation en attente, telle que l'Owner la voit dans la page Membres.</summary>
public sealed record InvitationDto(Guid Id, string Email, BandRole Role, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);

/// <summary>Résultat d'une création : le lien (montré une seule fois) et si l'e-mail est parti.</summary>
public sealed record InvitationCreatedDto(
    Guid Id, string Email, BandRole Role, DateTimeOffset ExpiresAt, string Link, bool EmailSent);

/// <summary>Ce que montre la page /invite/{jeton}, même sans compte.</summary>
public sealed record InvitationPreviewDto(string BandName, string Email, BandRole Role, DateTimeOffset ExpiresAt);