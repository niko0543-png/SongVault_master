using Microsoft.AspNetCore.Mvc;

using SongVault.Api.Auth;
using SongVault.Api.Contracts.Files;
using SongVault.Application.Files.Sync;

namespace SongVault.Api.Controllers;

/// <summary>Calage d'une tablature sur une maquette de la même version : le curseur suit l'enregistrement (ADR 0036).</summary>
[ApiController]
[Route("api/bands/{bandId:guid}/songs/{songId:guid}/versions/{versionId:guid}/files/{fileId:guid}/sync")]
[BandScoped]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
public sealed class TabSyncController : ControllerBase
{
    /// <summary>Le calage de la tablature.</summary>
    /// <response code="200">La maquette suivie et les points.</response>
    /// <response code="204">La tablature n'est pas calée, ou sa maquette est à la corbeille : elle se joue avec son propre son.</response>
    /// <response code="404">Version inconnue, ou fichier qui n'est pas une tablature de cette version.</response>
    [HttpGet]
    [ProducesResponseType<TabSyncResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Get(
        Guid songId, Guid versionId, Guid fileId, [FromServices] GetTabSyncHandler handler, CancellationToken ct)
        => await handler.HandleAsync(songId, versionId, fileId, ct) is { } sync
            ? Ok(TabSyncResponse.From(sync))
            : NoContent();

    /// <summary>Remplace le calage de la tablature.</summary>
    /// <response code="200">Calage enregistré.</response>
    /// <response code="400">Maquette ou points manquants, point hors limites, plus de 2000 points.</response>
    /// <response code="409">La version a été modifiée entre-temps ; rechargez-la.</response>
    /// <response code="422">Maquette pas audio, d'une autre version ou à la corbeille ; points dans le désordre ou en double.</response>
    [HttpPut]
    [ProducesResponseType<TabSyncResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<TabSyncResponse> Set(
        Guid songId, Guid versionId, Guid fileId, SetTabSyncRequest request,
        [FromServices] SetTabSyncHandler handler, CancellationToken ct)
        => TabSyncResponse.From(await handler.HandleAsync(request.ToCommand(songId, versionId, fileId), ct));

    /// <summary>Retire le calage : la tablature se joue de nouveau avec son propre son.</summary>
    /// <response code="204">Calage retiré (ou il n'y en avait pas).</response>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(
        Guid songId, Guid versionId, Guid fileId, [FromServices] DeleteTabSyncHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(songId, versionId, fileId, ct);
        return NoContent();
    }
}