using Microsoft.AspNetCore.Mvc;
using SongVault.Api.Contracts.Common;
using SongVault.Api.Contracts.Songs;
using SongVault.Application.Common.Exceptions;
using SongVault.Application.Songs.CreateSong;
using SongVault.Application.Songs.DeleteSong;
using SongVault.Application.Songs.GetSong;
using SongVault.Application.Songs.ListSongs;
using SongVault.Application.Songs.UpdateSong;

namespace SongVault.Api.Controllers;

[ApiController]
[Route("api/songs")]
public sealed class SongsController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<SongResponse>>> List(
        [FromQuery] ListSongsRequest request, [FromServices] ListSongsHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(new ListSongsQuery(request.Page, request.PageSize), ct);
        return Ok(new PagedResponse<SongResponse>(
            [.. result.Items.Select(SongResponse.From)],
            result.Page, result.PageSize, result.TotalCount, result.TotalPages));
    }


    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SongResponse>> GetById(
        Guid id, [FromServices] GetSongHandler handler, CancellationToken ct)
    {
        var song = await handler.HandleAsync(id, ct);
        return song is null ? NotFound() : Ok(SongResponse.From(song));
    }

    [HttpPost]
    public async Task<ActionResult<SongResponse>> Create(
        CreateSongRequest request, [FromServices] CreateSongHandler handler, CancellationToken ct)
    {
        var command = new CreateSongCommand(request.Title, request.Artist, request.Description);
        var song = await handler.HandleAsync(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = song.Id }, SongResponse.From(song));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
    Guid id, UpdateSongRequest request, [FromServices] UpdateSongHandler handler, CancellationToken ct)
    {
        try
        {
            await handler.HandleAsync(new UpdateSongCommand(id, request.Title, request.Artist, request.Description), ct);
            return NoContent();
        }
        catch (NotFoundException) { return NotFound(); }   // TEMPORAIRE : supprimé demain
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromServices] DeleteSongHandler handler, CancellationToken ct)
    {
        try
        {
            await handler.HandleAsync(id, ct);
            return NoContent();
        }
        catch (NotFoundException) { return NotFound(); }   // TEMPORAIRE : supprimé demain
    }
}