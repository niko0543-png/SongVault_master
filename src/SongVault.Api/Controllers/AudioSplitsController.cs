using Microsoft.AspNetCore.Mvc;

using SongVault.Api.Auth;
using SongVault.Api.Contracts.Files;
using SongVault.Application.Files.Splits;

namespace SongVault.Api.Controllers;

/// <summary>
/// Découpage d'un enregistrement en morceaux (ADR 0050) : chaque passage gardé devient une nouvelle version,
/// d'un morceau existant ou d'un nouveau morceau. Fait en arrière-plan ; l'original reste tel quel.
/// </summary>
[ApiController]
[Route("api/bands/{bandId:guid}/songs/{songId:guid}/versions/{versionId:guid}/files/{fileId:guid}/splits")]
[BandScoped]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
public sealed class AudioSplitsController : ControllerBase
{
    /// <summary>Demande le découpage du fichier audio en passages.</summary>
    /// <response code="202">Découpage en cours ; GET …/splits/latest suit son avancement.</response>
    /// <response code="400">Requête invalide.</response>
    /// <response code="402">Les passages ne tiennent pas dans le stockage du groupe.</response>
    /// <response code="409">Aperçu pas encore prêt, fichier pas déclaré sain, ou découpage déjà en cours.</response>
    /// <response code="422">Passages hors du fichier, trop courts, qui se chevauchent, ou morceau choisi introuvable.</response>
    [HttpPost]
    [ProducesResponseType<AudioSplitResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status402PaymentRequired)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        Guid songId, Guid versionId, Guid fileId, RequestAudioSplitRequest request,
        [FromServices] RequestAudioSplitHandler handler, CancellationToken ct)
        => Accepted(AudioSplitResponse.From(await handler.HandleAsync(request.ToCommand(songId, versionId, fileId), ct)));

    /// <summary>État du dernier découpage du fichier.</summary>
    /// <response code="200">Le dernier découpage : en cours, fait (avec les versions créées) ou en échec.</response>
    /// <response code="204">Ce fichier n'a jamais été découpé.</response>
    [HttpGet("latest")]
    [ProducesResponseType<AudioSplitResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Latest(
        Guid songId, Guid versionId, Guid fileId, [FromServices] GetLatestAudioSplitHandler handler, CancellationToken ct)
        => await handler.HandleAsync(songId, versionId, fileId, ct) is { } split ? Ok(AudioSplitResponse.From(split)) : NoContent();
}
