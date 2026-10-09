using Microsoft.AspNetCore.Mvc;

using SongVault.Api.Contracts.Versions;
using SongVault.Application.Versions;

namespace SongVault.Api.Controllers;

/// <summary>Versions successives d'un morceau.</summary>
[ApiController]
[Route("api/songs/{songId:guid}/versions")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
public sealed class SongVersionsController : ControllerBase
{
    /// <summary>Liste des versions d'un morceau, de la plus récente à la plus ancienne.</summary>
    /// <param name="songId">Identifiant du morceau.</param>
    /// <response code="200">Les versions (résumé, sans paroles ni fichiers).</response>
    /// <response code="404">Morceau introuvable.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<SongVersionSummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SongVersionSummaryResponse>>> List(
        Guid songId, [FromServices] ListSongVersionsHandler handler, CancellationToken ct)
        => Ok((await handler.HandleAsync(songId, ct)).Select(SongVersionSummaryResponse.From).ToList());

    /// <summary>Détail d'une version, avec ses fichiers.</summary>
    /// <param name="songId">Identifiant du morceau.</param>
    /// <param name="versionId">Identifiant de la version.</param>
    /// <response code="200">La version.</response>
    /// <response code="404">Morceau ou version introuvable.</response>
    [HttpGet("{versionId:guid}")]
    [ProducesResponseType<SongVersionResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SongVersionResponse>> GetById(
        Guid songId, Guid versionId, [FromServices] GetSongVersionHandler handler, CancellationToken ct)
        => Ok(SongVersionResponse.From(await handler.HandleAsync(songId, versionId, ct)));

    /// <summary>Crée une nouvelle version ; son numéro est attribué automatiquement.</summary>
    /// <param name="songId">Identifiant du morceau.</param>
    /// <response code="201">Version créée.</response>
    /// <response code="400">Requête invalide.</response>
    /// <response code="409">Deux créations simultanées ; réessayez.</response>
    /// <response code="422">Règle métier non respectée.</response>
    [HttpPost]
    [ProducesResponseType<SongVersionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SongVersionResponse>> Create(
      Guid songId, CreateSongVersionRequest request, [FromServices] CreateSongVersionHandler handler, CancellationToken ct)
    {
        var version = await handler.HandleAsync(new CreateSongVersionCommand(
            songId, request.Title, request.Status.GetValueOrDefault(), request.Notes, request.Lyrics,
            request.Bpm, request.Key), ct);
        return CreatedAtAction(nameof(GetById), new { songId, versionId = version.Id }, SongVersionResponse.From(version));
    }

    /// <summary>Modifie une version (titre, statut, notes, paroles).</summary>
    /// <param name="songId">Identifiant du morceau.</param>
    /// <param name="versionId">Identifiant de la version.</param>
    /// <response code="204">Version modifiée.</response>
    /// <response code="400">Requête invalide.</response>
    /// <response code="409">Modification concurrente.</response>
    /// <response code="422">Transition de statut interdite ou autre règle métier.</response>
    [HttpPut("{versionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
      Guid songId, Guid versionId, UpdateSongVersionRequest request,
      [FromServices] UpdateSongVersionHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(new UpdateSongVersionCommand(
            songId, versionId, request.Title, request.Status.GetValueOrDefault(), request.Notes, request.Lyrics,
            request.Bpm, request.Key), ct);
        return NoContent();
    }
}