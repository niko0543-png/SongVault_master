using Microsoft.AspNetCore.Mvc;

using SongVault.Api.Auth;
using SongVault.Api.Contracts.Releases;
using SongVault.Application.Releases.Exports;

namespace SongVault.Api.Controllers;

/// <summary>
/// Archive complète d'une sortie pour le distributeur (#24, ADR 0031) : fichiers audio des versions choisies,
/// pistes séparées, paroles, crédits et pochette. Préparée en arrière-plan, téléchargeable 24 h.
/// </summary>
[ApiController]
[Route("api/bands/{bandId:guid}/releases/{releaseId:guid}/exports")]
[BandScoped]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
public sealed class ReleaseExportsController : ControllerBase
{
    /// <summary>Demande l'archive ; un e-mail prévient quand elle est prête.</summary>
    /// <response code="202">Archive en préparation.</response>
    /// <response code="409">Une archive est déjà en préparation, ou la sortie n'est pas complète (sans piste, piste sans audio, fichier en cours d'analyse ou bloqué).</response>
    [HttpPost]
    [ProducesResponseType<ReleaseExportResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        Guid releaseId, [FromServices] RequestReleaseExportHandler handler, CancellationToken ct)
        => Accepted(ReleaseExportResponse.From(await handler.HandleAsync(releaseId, ct)));

    /// <summary>État de la dernière archive de la sortie.</summary>
    /// <response code="200">La dernière archive : en préparation, prête ou en échec.</response>
    /// <response code="204">Aucune archive, ou la dernière a expiré.</response>
    [HttpGet("latest")]
    [ProducesResponseType<ReleaseExportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Latest(
        Guid releaseId, [FromServices] GetLatestReleaseExportHandler handler, CancellationToken ct)
        => await handler.HandleAsync(releaseId, ct) is { } export ? Ok(ReleaseExportResponse.From(export)) : NoContent();

    /// <summary>Télécharge l'archive, si elle est prête.</summary>
    /// <response code="200">Le fichier ZIP (stockage local).</response>
    /// <response code="302">Redirection vers une adresse de lecture courte du stockage objet.</response>
    /// <response code="404">Archive introuvable, pas encore prête, ou en échec.</response>
    /// <response code="410">Le lien a expiré : préparez une nouvelle archive.</response>
    [HttpGet("{exportId:guid}/archive")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status410Gone)]
    public async Task<IActionResult> Download(
        Guid releaseId, Guid exportId, [FromServices] DownloadReleaseExportHandler handler, CancellationToken ct)
        => this.ToResult(await handler.HandleAsync(releaseId, exportId, ct), asAttachment: true, enableRangeProcessing: false);
}