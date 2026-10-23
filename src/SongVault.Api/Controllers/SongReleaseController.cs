using Microsoft.AspNetCore.Mvc;

using SongVault.Api.Auth;
using SongVault.Api.Contracts.Releases;
using SongVault.Application.Releases;

namespace SongVault.Api.Controllers;

/// <summary>La sortie d'un morceau (#38, ADR 0044) : ses infos de sortie l'envoient dans l'onglet Sorties.</summary>
[ApiController]
[Route("api/bands/{bandId:guid}/songs/{songId:guid}/release")]
[BandScoped]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
public sealed class SongReleaseController : ControllerBase
{
    /// <summary>Enregistre les infos de sortie de la dernière version (Final) : le morceau sort.</summary>
    /// <remarks>
    /// Keep = LatestOnly met les autres versions à la corbeille, sauf celles qu'une autre sortie ou une setlist utilise
    /// (renvoyées dans KeptVersions). PurgeNow les efface tout de suite : réservé aux propriétaires.
    /// </remarks>
    /// <response code="200">Le morceau est sorti.</response>
    /// <response code="403">PurgeNow demandé par un membre qui n'est pas propriétaire.</response>
    /// <response code="409">Le morceau est déjà sorti.</response>
    /// <response code="422">Pas la dernière version, pas au statut Final, lien ou code-barres invalide.</response>
    [HttpPost]
    [ProducesResponseType<SongReleaseResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<SongReleaseResponse> Release(
        Guid songId, ReleaseSongRequest request, [FromServices] ReleaseSongHandler handler, CancellationToken ct)
        => SongReleaseResponse.From(await handler.HandleAsync(request.ToCommand(songId), ct));

    /// <summary>Remet le morceau dans les morceaux en travail. La sortie et sa piste restent.</summary>
    /// <response code="204">Morceau remis en travail (ou déjà en travail).</response>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Unrelease(Guid songId, [FromServices] UnreleaseSongHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(songId, ct);
        return NoContent();
    }
}
