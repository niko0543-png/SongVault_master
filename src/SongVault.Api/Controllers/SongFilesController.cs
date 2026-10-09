using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

using SongVault.Api.Contracts.Files;
using SongVault.Api.Security;
using SongVault.Application.Files;

namespace SongVault.Api.Controllers;

/// <summary>Fichiers (audio, partitions, images) rattachés à une version.</summary>
[ApiController]
[Route("api/songs/{songId:guid}/versions/{versionId:guid}/files")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
public sealed class SongFilesController : ControllerBase
{
    /// <summary>Envoie un fichier et le rattache à la version.</summary>
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
        Guid songId, Guid versionId, IFormFile file,
        [FromServices] UploadSongFileHandler handler, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var result = await handler.HandleAsync(
            new UploadSongFileCommand(songId, versionId, file.FileName, file.Length, stream), ct);

        return CreatedAtAction(nameof(Download), new { songId, versionId, fileId = result.Id },
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
    [HttpGet("{fileId:guid}/content")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status206PartialContent)]
    public async Task<IActionResult> Download(
        Guid songId, Guid versionId, Guid fileId, [FromQuery] bool download,
        [FromServices] DownloadSongFileHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(songId, versionId, fileId, ct);
        // download=true → Content-Disposition: attachment ; sinon lecture dans le navigateur (lecteur audio en S4)
        return File(result.Content, result.ContentType, download ? result.FileName : null, enableRangeProcessing: true);
    }

    /// <summary>Supprime un fichier de la version.</summary>
    /// <param name="songId">Identifiant du morceau.</param>
    /// <param name="versionId">Identifiant de la version.</param>
    /// <param name="fileId">Identifiant du fichier.</param>
    /// <response code="204">Fichier supprimé.</response>
    [HttpDelete("{fileId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(
        Guid songId, Guid versionId, Guid fileId, [FromServices] DeleteSongFileHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(songId, versionId, fileId, ct);
        return NoContent();
    }
}