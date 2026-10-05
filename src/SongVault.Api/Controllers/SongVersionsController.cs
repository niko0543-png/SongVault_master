using Microsoft.AspNetCore.Mvc;

using SongVault.Api.Contracts.Versions;
using SongVault.Application.Versions;

namespace SongVault.Api.Controllers;

[ApiController]
[Route("api/songs/{songId:guid}/versions")]
public sealed class SongVersionsController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SongVersionSummaryResponse>>> List(
        Guid songId, [FromServices] ListSongVersionsHandler handler, CancellationToken ct)
        => Ok((await handler.HandleAsync(songId, ct)).Select(SongVersionSummaryResponse.From).ToList());

    [HttpGet("{versionId:guid}")]
    public async Task<ActionResult<SongVersionResponse>> GetById(
        Guid songId, Guid versionId, [FromServices] GetSongVersionHandler handler, CancellationToken ct)
        => Ok(SongVersionResponse.From(await handler.HandleAsync(songId, versionId, ct)));

    [HttpPost]
    public async Task<ActionResult<SongVersionResponse>> Create(
      Guid songId, CreateSongVersionRequest request, [FromServices] CreateSongVersionHandler handler, CancellationToken ct)
    {
        var version = await handler.HandleAsync(new CreateSongVersionCommand(
            songId, request.Title, request.Status.GetValueOrDefault(), request.Notes, request.Lyrics,
            request.Bpm, request.Key), ct);
        return CreatedAtAction(nameof(GetById), new { songId, versionId = version.Id }, SongVersionResponse.From(version));
    }

    [HttpPut("{versionId:guid}")]
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