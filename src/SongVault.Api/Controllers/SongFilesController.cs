using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SongVault.Api.Auth;
using SongVault.Api.Contracts.Files;
using SongVault.Api.Security;
using SongVault.Application.Files;

namespace SongVault.Api.Controllers;

/// <summary>Fichiers (audio, partitions, images) rattachés à une version.</summary>
[ApiController]
[Route("api/bands/{bandId:guid}/songs/{songId:guid}/versions/{versionId:guid}/files")]
[BandScoped]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
public sealed class SongFilesController : ControllerBase
{
    /// <summary>Envoie un fichier de 50 Mo au plus à travers l'API et le rattache à la version.</summary>
    /// <remarks>
    /// Extensions acceptées et taille maximale : voir GET /api/files/policy.
    /// Le contenu réel est contrôlé (signature) : un .exe renommé en .mp3 est refusé.
    /// </remarks>
    /// <param name="songId">Identifiant du morceau.</param>
    /// <param name="versionId">Identifiant de la version.</param>
    /// <param name="file">Le fichier à envoyer.</param>
    /// <response code="201">Fichier enregistré ; Location pointe vers son contenu.</response>
    /// <response code="400">Extension non autorisée ou contenu ne correspondant pas à l'extension.</response>
    /// <response code="413">Fichier trop volumineux.</response>
    /// <response code="429">Trop d'envois en peu de temps ; réessayez dans une minute.</response>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<SongFileResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [RequestSizeLimit(FileTypePolicy.MaxRequestSizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = FileTypePolicy.MaxRequestSizeBytes)]
    [EnableRateLimiting(RateLimiting.Uploads)]
    public async Task<ActionResult<SongFileResponse>> Upload(
        Guid bandId, Guid songId, Guid versionId, IFormFile file,
        [FromServices] UploadSongFileHandler handler, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var result = await handler.HandleAsync(
            new UploadSongFileCommand(songId, versionId, file.FileName, file.Length, stream), ct);

        return CreatedAtAction(nameof(Download), new { bandId, songId, versionId, fileId = result.Id },
            SongFileResponse.From(result));
    }

    /// <summary>Annonce l'envoi direct d'un fichier (jusqu'à 2 Go) et renvoie l'adresse où l'envoyer.</summary>
    /// <remarks>
    /// Parcours en trois temps : cette annonce, un PUT du fichier à UploadUrl avec Headers, puis
    /// POST …/uploads/{uploadId}/complete. L'adresse est valable une heure.
    /// </remarks>
    /// <response code="200">Adresse d'envoi.</response>
    /// <response code="400">Extension non autorisée ou fichier vide.</response>
    /// <response code="413">Fichier trop volumineux.</response>
    /// <response code="429">Trop d'envois en peu de temps ; réessayez dans une minute.</response>
    [HttpPost("uploads")]
    [ProducesResponseType<FileUploadResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [EnableRateLimiting(RateLimiting.Uploads)]
    public async Task<ActionResult<FileUploadResponse>> StartUpload(
        Guid bandId, Guid songId, Guid versionId, StartFileUploadRequest request,
        [FromServices] StartFileUploadHandler handler, CancellationToken ct)
    {
        var ticket = await handler.HandleAsync(
            new StartFileUploadCommand(songId, versionId, request.FileName, request.SizeBytes), ct);

        // Stockage local : le navigateur envoie le fichier à l'API, sur la route ci-dessous
        var url = ticket.Direct?.Url.ToString()
                  ?? Url.Action(nameof(ReceiveUpload), new { bandId, songId, versionId, uploadId = ticket.UploadId })!;
        return Ok(new FileUploadResponse(ticket.UploadId, url,
            ticket.Direct?.Headers ?? new Dictionary<string, string>(), ticket.ExpiresAt));
    }

    /// <summary>Stockage local seulement : reçoit le contenu d'un envoi annoncé.</summary>
    /// <response code="204">Fichier reçu ; il reste à confirmer l'envoi.</response>
    /// <response code="400">Taille différente de la taille annoncée.</response>
    /// <response code="409">Fichier déjà reçu.</response>
    [HttpPut("uploads/{uploadId:guid}/content")]
    [DisableRequestSizeLimit]                                   // la taille annoncée (2 Go au plus) fait foi
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReceiveUpload(
        Guid songId, Guid versionId, Guid uploadId, [FromServices] ReceiveFileUploadHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(songId, versionId, uploadId, Request.ContentLength, Request.Body, ct);
        return NoContent();
    }

    /// <summary>Confirme un envoi : contrôle la taille et la signature, puis rattache le fichier à la version.</summary>
    /// <response code="201">Fichier rattaché ; Location pointe vers son contenu.</response>
    /// <response code="400">Taille ou contenu différents de ce qui a été annoncé ; le fichier est effacé.</response>
    /// <response code="409">Le fichier n'est pas encore arrivé.</response>
    [HttpPost("uploads/{uploadId:guid}/complete")]
    [ProducesResponseType<SongFileResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SongFileResponse>> CompleteUpload(
        Guid bandId, Guid songId, Guid versionId, Guid uploadId,
        [FromServices] CompleteFileUploadHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(songId, versionId, uploadId, ct);
        return CreatedAtAction(nameof(Download), new { bandId, songId, versionId, fileId = result.Id },
            SongFileResponse.From(result));
    }


    /// <summary>Contenu du fichier, pour lecture dans le navigateur ou téléchargement.</summary>
    /// <remarks>Prend en charge l'en-tête Range (lecture audio avec déplacement dans le morceau).</remarks>
    /// <param name="songId">Identifiant du morceau.</param>
    /// <param name="versionId">Identifiant de la version.</param>
    /// <param name="fileId">Identifiant du fichier.</param>
    /// <param name="download">true : propose l'enregistrement sous son nom d'origine.</param>
    /// <response code="200">Le contenu complet.</response>
    /// <response code="206">Une partie du contenu (requête Range).</response>
    /// <response code="302">Stockage Azure Blob : redirection vers une URL signée, valable 30 minutes.</response>
    ///  /// <response code="409">Analyse antivirus en cours, en échec, ou fichier bloqué (#17).</response>
    [HttpGet("{fileId:guid}/content")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status206PartialContent)]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Download(
        Guid songId, Guid versionId, Guid fileId, [FromQuery] bool download,
        [FromServices] DownloadSongFileHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(songId, versionId, fileId, download, ct);
        // download=true → Content-Disposition: attachment ; sinon lecture dans le navigateur (lecteur audio en S4)
        return this.ToResult(result, asAttachment: download, enableRangeProcessing: true);
    }

    /// <summary>Fichier à écouter : le MP3 léger de l'aperçu s'il est prêt, sinon l'original (#15).</summary>
    /// <remarks>Prend en charge l'en-tête Range. Le téléchargement reste celui de l'original (…/content?download=true).</remarks>
    /// <param name="songId">Identifiant du morceau.</param>
    /// <param name="versionId">Identifiant de la version.</param>
    /// <param name="fileId">Identifiant du fichier.</param>
    /// <response code="200">Le contenu complet.</response>
    /// <response code="206">Une partie du contenu (requête Range).</response>
    /// <response code="302">Stockage Azure Blob : redirection vers une URL signée, valable 30 minutes.</response>
    ///  /// <response code="409">Analyse antivirus en cours, en échec, ou fichier bloqué (#17).</response>
    [HttpGet("{fileId:guid}/listen")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status206PartialContent)]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Listen(
        Guid songId, Guid versionId, Guid fileId, [FromServices] ListenSongFileHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(songId, versionId, fileId, ct);
        return this.ToResult(result, asAttachment: false, enableRangeProcessing: true);
    }

    /// <summary>Forme d'onde précalculée d'un fichier audio (#15).</summary>
    /// <param name="songId">Identifiant du morceau.</param>
    /// <param name="versionId">Identifiant de la version.</param>
    /// <param name="fileId">Identifiant du fichier.</param>
    /// <response code="200">La forme d'onde ; elle ne change plus, le navigateur la garde un jour.</response>
    /// <response code="404">Fichier inconnu, pas audio, ou aperçu pas encore prêt.</response>
    [HttpGet("{fileId:guid}/waveform")]
    [ProducesResponseType<WaveformResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<WaveformResponse>> Waveform(
        Guid songId, Guid versionId, Guid fileId, [FromServices] GetWaveformHandler handler, CancellationToken ct)
    {
        var waveform = await handler.HandleAsync(songId, versionId, fileId, ct);
        Response.Headers.CacheControl = "private, max-age=86400";
        return Ok(WaveformResponse.From(waveform));
    }

    /// <summary>Relance la préparation d'un aperçu en échec (#15).</summary>
    /// <param name="songId">Identifiant du morceau.</param>
    /// <param name="versionId">Identifiant de la version.</param>
    /// <param name="fileId">Identifiant du fichier.</param>
    /// <response code="202">Aperçu de nouveau en préparation.</response>
    /// <response code="404">Fichier inconnu ou pas audio.</response>
    /// <response code="409">L'aperçu est déjà prêt ou en préparation, ou le fichier n'est pas déclaré sain (#17).</response>
    [HttpPost("{fileId:guid}/preview")]
    [ProducesResponseType<AudioPreviewResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AudioPreviewResponse>> RetryPreview(
        Guid songId, Guid versionId, Guid fileId, [FromServices] RetryAudioPreviewHandler handler, CancellationToken ct)
        => Accepted(new AudioPreviewResponse(await handler.HandleAsync(songId, versionId, fileId, ct)));

    /// <summary>Relance l'analyse antivirus d'un fichier en échec (#17).</summary>
    /// <param name="songId">Identifiant du morceau.</param>
    /// <param name="versionId">Identifiant de la version.</param>
    /// <param name="fileId">Identifiant du fichier.</param>
    /// <response code="202">Analyse de nouveau en cours.</response>
    /// <response code="409">L'analyse est en cours, ou le fichier est déjà déclaré sain ou infecté.</response>
    [HttpPost("{fileId:guid}/scan")]
    [ProducesResponseType<FileScanResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FileScanResponse>> RetryScan(
        Guid songId, Guid versionId, Guid fileId, [FromServices] RetryFileScanHandler handler, CancellationToken ct)
        => Accepted(new FileScanResponse(await handler.HandleAsync(songId, versionId, fileId, ct)));

    /// <summary>Met un fichier de la version à la corbeille (#20) ; restaurable 30 jours.</summary>
    /// <param name="songId">Identifiant du morceau.</param>
    /// <param name="versionId">Identifiant de la version.</param>
    /// <param name="fileId">Identifiant du fichier.</param>
    /// <response code="204">Fichier à la corbeille.</response>
    [HttpDelete("{fileId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(
        Guid songId, Guid versionId, Guid fileId, [FromServices] DeleteSongFileHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(songId, versionId, fileId, ct);
        return NoContent();
    }
}