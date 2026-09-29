using Microsoft.AspNetCore.Mvc;

using SongVault.Api.Contracts.Files;
using SongVault.Application.Files;

namespace SongVault.Api.Controllers;

[ApiController]
[Route("api/songs/{songId:guid}/versions/{versionId:guid}/files")]
public sealed class SongFilesController : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(FileTypePolicy.MaxRequestSizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = FileTypePolicy.MaxRequestSizeBytes)]
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

    [HttpGet("{fileId:guid}/content")]
    public async Task<IActionResult> Download(
        Guid songId, Guid versionId, Guid fileId, [FromQuery] bool download,
        [FromServices] DownloadSongFileHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(songId, versionId, fileId, ct);
        // download=true → Content-Disposition: attachment ; sinon lecture dans le navigateur (lecteur audio en S4)
        return File(result.Content, result.ContentType, download ? result.FileName : null, enableRangeProcessing: true);
    }

    [HttpDelete("{fileId:guid}")]
    public async Task<IActionResult> Delete(
        Guid songId, Guid versionId, Guid fileId, [FromServices] DeleteSongFileHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(songId, versionId, fileId, ct);
        return NoContent();
    }
}