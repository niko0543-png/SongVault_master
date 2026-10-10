using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

using SongVault.Api.Contracts.Bands;
using SongVault.Api.Contracts.Invitations;
using SongVault.Api.Security;
using SongVault.Application.Invitations;

namespace SongVault.Api.Controllers;

/// <summary>Lien d'invitation reçu par e-mail : aperçu, puis acceptation (ADR 0009).</summary>
[ApiController]
[Route("api/invitations")]
[EnableRateLimiting(RateLimiting.Auth)]              // même limite par IP que la connexion
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status410Gone)]
public sealed class InvitationsController : ControllerBase
{
    /// <summary>Groupe, adresse et rôle de l'invitation. Accessible sans être connecté.</summary>
    [HttpGet("{token}"), AllowAnonymous]
    [ProducesResponseType<InvitationPreviewResponse>(StatusCodes.Status200OK)]
    public async Task<InvitationPreviewResponse> Preview(
        string token, [FromServices] PreviewInvitationHandler handler, CancellationToken ct)
        => InvitationPreviewResponse.From(await handler.HandleAsync(token, ct));

    /// <summary>Rejoint le groupe avec le compte connecté, qui doit avoir l'adresse invitée.</summary>
    /// <response code="200">Le groupe rejoint, avec votre rôle.</response>
    /// <response code="403">Vous êtes connecté avec une autre adresse que celle invitée.</response>
    /// <response code="409">Acceptation simultanée : réessayez.</response>
    [HttpPost("{token}/accept")]
    [ProducesResponseType<BandResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<BandResponse> Accept(string token, [FromServices] AcceptInvitationHandler handler, CancellationToken ct)
        => BandResponse.From(await handler.HandleAsync(token, ct));
}