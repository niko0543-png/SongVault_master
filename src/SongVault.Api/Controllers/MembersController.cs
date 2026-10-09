using Microsoft.AspNetCore.Mvc;

using SongVault.Api.Auth;
using SongVault.Api.Contracts.Bands;
using SongVault.Application.Bands;
using SongVault.Domain.Bands;

namespace SongVault.Api.Controllers;

/// <summary>Membres du groupe et leurs rôles (ADR 0008).</summary>
[ApiController]
[Route("api/bands/{bandId:guid}/members")]
[BandScoped]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
public sealed class MembersController : ControllerBase
{
    /// <summary>Membres du groupe, triés par e-mail. Accessible à tout membre.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<BandMemberResponse>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<BandMemberResponse>> List(
        [FromServices] ListMembersHandler handler, CancellationToken ct)
        => [.. (await handler.HandleAsync(ct)).Select(BandMemberResponse.From)];

    /// <summary>Change le rôle d'un membre. Réservé aux Owner.</summary>
    /// <response code="204">Rôle changé.</response>
    /// <response code="403">Vous n'êtes pas Owner du groupe.</response>
    /// <response code="404">Groupe inconnu, ou utilisateur qui n'en est pas membre.</response>
    /// <response code="409">C'est le dernier Owner : il ne peut pas perdre ce rôle.</response>
    [HttpPut("{userId}/role"), MinimumBandRole(BandRole.Owner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeRole(
        string userId, ChangeMemberRoleRequest request, [FromServices] ChangeMemberRoleHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(userId, request.Role.GetValueOrDefault(), ct);
        return NoContent();
    }

    /// <summary>Quitter le groupe. Ouvert à tout membre, Guest compris.</summary>
    /// <response code="204">Vous n'êtes plus membre du groupe.</response>
    /// <response code="409">Vous êtes le dernier Owner : nommez d'abord un autre Owner.</response>
    [HttpDelete("me"), MinimumBandRole(BandRole.Guest)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Leave([FromServices] LeaveBandHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(ct);
        return NoContent();
    }

    /// <summary>Retire un membre du groupe. Réservé aux Owner.</summary>
    /// <response code="204">Membre retiré.</response>
    /// <response code="403">Vous n'êtes pas Owner du groupe.</response>
    /// <response code="409">C'est le dernier Owner du groupe.</response>
    [HttpDelete("{userId}"), MinimumBandRole(BandRole.Owner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Remove(string userId, [FromServices] RemoveMemberHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(userId, ct);
        return NoContent();
    }
}