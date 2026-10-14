using Microsoft.AspNetCore.Mvc;

using SongVault.Api.Auth;
using SongVault.Api.Contracts.Activity;
using SongVault.Application.Activity;

namespace SongVault.Api.Controllers;

/// <summary>Fil d'activité du groupe (ADR 0017). Lisible par tout membre, Guest compris.</summary>
[ApiController]
[Route("api/bands/{bandId:guid}/activity")]
[BandScoped]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public sealed class ActivityController : ControllerBase
{
    /// <summary>Événements du plus récent au plus ancien, page par page.</summary>
    /// <remarks>Pour la page suivante, renvoyer NextCursor dans cursor. Un événement arrivé entre-temps ne décale rien.</remarks>
    /// <response code="200">Une page d'événements (éventuellement vide).</response>
    /// <response code="400">Curseur illisible ou taille de page hors limites.</response>
    [HttpGet]
    [ProducesResponseType<ActivityPageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ActivityPageResponse>> List(
        [FromQuery] ListActivityRequest request, [FromServices] ListActivityHandler handler, CancellationToken ct)
    {
        ActivityCursor? cursor = null;
        if (request.Cursor is not null)
        {
            if (!ActivityCursor.TryParse(request.Cursor, out var parsed))
            {
                ModelState.AddModelError(nameof(request.Cursor), "Curseur illisible : reprenez le fil depuis le début.");
                return ValidationProblem(ModelState);
            }
            cursor = parsed;
        }

        var page = await handler.HandleAsync(cursor, request.PageSize, ct);
        return Ok(new ActivityPageResponse([.. page.Items.Select(ActivityItemResponse.From)], page.NextCursor));
    }
}