using Microsoft.AspNetCore.Mvc;

using SongVault.Api.Auth;
using SongVault.Api.Contracts.Invitations;
using SongVault.Application.Invitations;
using SongVault.Domain.Bands;

namespace SongVault.Api.Controllers;

/// <summary>Invitations du groupe, gérées par ses Owner (ADR 0009).</summary>
[ApiController]
[Route("api/bands/{bandId:guid}/invitations")]
[BandScoped, MinimumBandRole(BandRole.Owner)]       // GET compris : la liste montre des adresses
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
public sealed class BandInvitationsController : ControllerBase
{
    /// <summary>Invitations en attente, les plus récentes d'abord.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<InvitationResponse>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<InvitationResponse>> List([FromServices] ListInvitationsHandler handler, CancellationToken ct)
        => [.. (await handler.HandleAsync(ct)).Select(InvitationResponse.From)];

    /// <summary>Invite une adresse ; envoie l'e-mail et renvoie le lien (une seule fois).</summary>
    /// <response code="201">Invitation créée ; emailSent dit si l'e-mail est parti.</response>
    /// <response code="422">Rôle Owner, ou adresse déjà membre du groupe.</response>
    [HttpPost]
    [ProducesResponseType<InvitationCreatedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<InvitationCreatedResponse>> Create(
        CreateInvitationRequest request, [FromServices] CreateInvitationHandler handler, CancellationToken ct)
    {
        var created = await handler.HandleAsync(new CreateInvitationCommand(request.Email, request.Role.GetValueOrDefault()), ct);
        return StatusCode(StatusCodes.Status201Created, InvitationCreatedResponse.From(created));
    }

    /// <summary>Annule une invitation en attente : son lien ne marche plus.</summary>
    /// <response code="204">Invitation annulée.</response>
    /// <response code="410">Déjà utilisée, annulée ou expirée.</response>
    [HttpDelete("{invitationId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status410Gone)]
    public async Task<IActionResult> Revoke(Guid invitationId, [FromServices] RevokeInvitationHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(invitationId, ct);
        return NoContent();
    }
}