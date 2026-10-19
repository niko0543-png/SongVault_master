using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SongVault.Api.Auth;
using SongVault.Api.Contracts.Releases;
using SongVault.Api.Security;
using SongVault.Application.Releases;
using SongVault.Domain.Releases;
using SongVault.Application.Songs.Rights;

namespace SongVault.Api.Controllers;

/// <summary>Sorties du groupe (single, EP, album), leurs pistes et leur pochette (ADR 0016).</summary>
[ApiController]
[Route("api/bands/{bandId:guid}/releases")]
[BandScoped]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
public sealed class ReleasesController : ControllerBase
{
    /// <summary>Sorties du groupe : en préparation (sans date) d'abord, puis de la plus récente à la plus ancienne.</summary>
    /// <response code="200">Les sorties (éventuellement aucune).</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ReleaseSummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<ReleaseSummaryResponse>> List(
        [FromServices] ListReleasesHandler handler, CancellationToken ct)
        => [.. (await handler.HandleAsync(ct)).Select(ReleaseSummaryResponse.From)];

    /// <summary>Détail d'une sortie et ses pistes dans l'ordre.</summary>
    /// <response code="200">La sortie.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ReleaseResponse>(StatusCodes.Status200OK)]
    public async Task<ReleaseResponse> GetById(Guid id, [FromServices] GetReleaseHandler handler, CancellationToken ct)
        => ReleaseResponse.From(await handler.HandleAsync(id, ct));

    /// <summary>Crée une sortie sans piste.</summary>
    /// <response code="201">Sortie créée ; Location pointe vers son détail.</response>
    /// <response code="400">Titre manquant ou trop long, type inconnu.</response>
    [HttpPost]
    [ProducesResponseType<ReleaseResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ReleaseResponse>> Create(
        Guid bandId, SaveReleaseRequest request, [FromServices] CreateReleaseHandler handler, CancellationToken ct)
    {
        var release = await handler.HandleAsync(
            new CreateReleaseCommand(request.Title, request.Type!.Value, request.ReleaseDate), ct);
        return CreatedAtAction(nameof(GetById), new { bandId, id = release.Id }, ReleaseResponse.From(release));
    }

    /// <summary>Modifie le titre, le type et la date de sortie.</summary>
    /// <response code="204">Sortie modifiée.</response>
    /// <response code="409">La sortie a été modifiée entre-temps ; rechargez-la.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        Guid id, SaveReleaseRequest request, [FromServices] UpdateReleaseHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(new UpdateReleaseCommand(id, request.Title, request.Type!.Value, request.ReleaseDate), ct);
        return NoContent();
    }

    /// <summary>Supprime la sortie, ses pistes et sa pochette. Les morceaux et leurs versions restent.</summary>
    /// <response code="204">Sortie supprimée.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, [FromServices] DeleteReleaseHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(id, ct);
        return NoContent();
    }

    /// <summary>Remplace toutes les pistes par la liste envoyée, dans son ordre (ajouter, retirer, réordonner, changer de version).</summary>
    /// <remarks>Un morceau figure au plus une fois ; chaque version doit appartenir à son morceau, dans le groupe.</remarks>
    /// <response code="200">La sortie avec ses nouvelles pistes.</response>
    /// <response code="409">Pistes modifiées entre-temps par quelqu'un d'autre ; rechargez la sortie.</response>
    /// <response code="422">Morceau en double, version d'un autre morceau, ou plus de 100 pistes.</response>
    [HttpPut("{id:guid}/tracks")]
    [ProducesResponseType<ReleaseResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ReleaseResponse> SetTracks(
        Guid id, SetReleaseTracksRequest request, [FromServices] SetReleaseTracksHandler handler, CancellationToken ct)
    {
        var tracks = request.Tracks.Select(t => new ReleaseTrackChoice(t.SongId!.Value, t.VersionId!.Value)).ToList();
        return ReleaseResponse.From(await handler.HandleAsync(new SetReleaseTracksCommand(id, tracks), ct));
    }

    /// <summary>Envoie ou remplace la pochette : JPEG, PNG ou WebP de 5 Mo au plus.</summary>
    /// <response code="204">Pochette enregistrée.</response>
    /// <response code="400">Format non accepté, ou contenu qui ne correspond pas à l'extension.</response>
    /// <response code="413">Image trop volumineuse.</response>
    /// <response code="429">Trop d'envois en peu de temps ; réessayez dans une minute.</response>
    [HttpPut("{id:guid}/cover")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [RequestSizeLimit(CoverImagePolicy.MaxRequestSizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = CoverImagePolicy.MaxRequestSizeBytes)]
    [EnableRateLimiting(RateLimiting.Uploads)]
    public async Task<IActionResult> UploadCover(
        Guid id, IFormFile file, [FromServices] UploadReleaseCoverHandler handler, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        await handler.HandleAsync(new UploadReleaseCoverCommand(id, file.FileName, file.Length, stream), ct);
        return NoContent();
    }

    /// <summary>Image de la pochette, à afficher dans le navigateur.</summary>
    /// <response code="200">L'image (stockage local).</response>
    /// <response code="302">Redirection vers le stockage objet.</response>
    /// <response code="404">Sortie introuvable ou sans pochette.</response>
    [HttpGet("{id:guid}/cover")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public async Task<IActionResult> DownloadCover(
        Guid id, [FromServices] DownloadReleaseCoverHandler handler, CancellationToken ct)
        => this.ToResult(await handler.HandleAsync(id, ct), asAttachment: false, enableRangeProcessing: false);

    /// <summary>Crédits de toutes les pistes en CSV (#22) : une ligne par ayant droit, avec ISRC et ISWC.</summary>
    /// <response code="200">Le fichier credits-Titre.csv.</response>
    [HttpGet("{id:guid}/credits.csv")]
    [Produces("text/csv")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportCredits(
        Guid id, [FromServices] ExportReleaseCreditsHandler handler, CancellationToken ct)
        => this.ToResult(await handler.HandleAsync(id, ct), asAttachment: true, enableRangeProcessing: false);

    /// <summary>Retire la pochette.</summary>
    /// <response code="204">Pochette retirée (ou déjà absente).</response>
    [HttpDelete("{id:guid}/cover")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteCover(
        Guid id, [FromServices] DeleteReleaseCoverHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(id, ct);
        return NoContent();
    }
}