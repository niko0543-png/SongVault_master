using Microsoft.AspNetCore.Mvc;

using SongVault.Api.Contracts.Songs;
using SongVault.Application.Songs.CreateSong;
using SongVault.Application.Songs.GetSong;
using SongVault.Application.Songs.ListSongs;

namespace SongVault.Api.Controllers;

[ApiController]
[Route("api/songs")]
public sealed class SongsController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SongResponse>>> List(
        [FromServices] ListSongsHandler handler, CancellationToken ct)
    {
        var songs = await handler.HandleAsync(ct);
        return Ok(songs.Select(SongResponse.From).ToList());
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
}