using Microsoft.AspNetCore.Mvc;

using SongVault.Api.Auth;
using SongVault.Api.Contracts.Audit;
using SongVault.Application.Activity;
using SongVault.Application.Audit;
using SongVault.Domain.Bands;

namespace SongVault.Api.Controllers;

/// <summary>
/// Journal d'audit du groupe (ADR 0026) : qui a supprimé quoi, qui a invité ou retiré qui. Lecture seule,
/// réservée aux Owner. Il n'existe aucune route pour modifier ou supprimer une ligne.
/// </summary>
[ApiController]
[Route("api/bands/{bandId:guid}/audit")]
[BandScoped, MinimumBandRole(BandRole.Owner)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
public sealed class AuditController : ControllerBase
{
    /// <summary>Lignes de la plus récente à la plus ancienne, page par page (un an au plus).</summary>
    /// <remarks>Pour la page suivante, renvoyer NextCursor dans cursor.</remarks>
    /// <response code="200">Une page du journal (éventuellement vide).</response>
    /// <response code="400">Curseur illisible ou taille de page hors limites.</response>
    /// <response code="403">Vous n'êtes pas Owner du groupe.</response>
    [HttpGet]
    [ProducesResponseType<AuditPageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuditPageResponse>> List(
        [FromQuery] ListAuditRequest request, [FromServices] ListAuditHandler handler, CancellationToken ct)
    {
        ActivityCursor? cursor = null;
        if (request.Cursor is not null)
        {
            if (!ActivityCursor.TryParse(request.Cursor, out var parsed))
            {
                ModelState.AddModelError(nameof(request.Cursor), "Curseur illisible : reprenez le journal depuis le début.");
                return ValidationProblem(ModelState);
            }
            cursor = parsed;
        }

        var page = await handler.HandleAsync(cursor, request.PageSize, ct);
        return Ok(new AuditPageResponse([.. page.Items.Select(AuditItemResponse.From)], page.NextCursor));
    }
}