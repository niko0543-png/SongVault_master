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

/// <summary>Morceaux de l'utilisateur connecté.</summary>
[ApiController]
[Route("api/songs")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public sealed class SongsController : ControllerBase
{
    /// <summary>Liste paginée des morceaux.</summary>
    /// <remarks>Seuls les morceaux de l'utilisateur connecté sont renvoyés.</remarks>
    /// <response code="200">Une page de morceaux (éventuellement vide).</response>
    /// <response code="400">Pagination invalide (page &lt; 1, taille hors limites).</response>
    [HttpGet]
    [ProducesResponseType<PagedResponse<SongResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<SongResponse>>> List(
        [FromQuery] ListSongsRequest request, [FromServices] ListSongsHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(new ListSongsQuery(request.Page, request.PageSize, request.Search, request.Status), ct);
        return Ok(new PagedResponse<SongResponse>(
            [.. result.Items.Select(SongResponse.From)],
            result.Page, result.PageSize, result.TotalCount, result.TotalPages));
    }


    /// <summary>Détail d'un morceau.</summary>
    /// <param name="id">Identifiant du morceau.</param>
    /// <response code="200">Le morceau.</response>
    /// <response code="404">Morceau inexistant ou appartenant à un autre utilisateur.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<SongResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SongResponse>> GetById(
        Guid id, [FromServices] GetSongHandler handler, CancellationToken ct)
        => Ok(SongResponse.From(await handler.HandleAsync(id, ct)));

    /// <summary>Crée un morceau appartenant à l'utilisateur connecté.</summary>
    /// <response code="201">Morceau créé ; l'en-tête Location pointe vers sa ressource.</response>
    /// <response code="400">Requête invalide (titre manquant, longueurs dépassées).</response>
    /// <response code="422">Règle métier non respectée.</response>
    [HttpPost]
    [ProducesResponseType<SongResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SongResponse>> Create(
        CreateSongRequest request, [FromServices] CreateSongHandler handler, CancellationToken ct)
    {
        var command = new CreateSongCommand(request.Title, request.Artist, request.Description);
        var song = await handler.HandleAsync(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = song.Id }, SongResponse.From(song));
    }


    /// <summary>Modifie le titre, l'artiste et la description d'un morceau.</summary>
    /// <param name="id">Identifiant du morceau.</param>
    /// <response code="204">Morceau modifié.</response>
    /// <response code="400">Requête invalide.</response>
    /// <response code="404">Morceau introuvable.</response>
    /// <response code="409">Le morceau a été modifié entre-temps ; rechargez-le.</response>
    /// <response code="422">Règle métier non respectée.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
    Guid id, UpdateSongRequest request, [FromServices] UpdateSongHandler handler, CancellationToken ct)
    { 
        await handler.HandleAsync(new UpdateSongCommand(id, request.Title, request.Artist, request.Description), ct);
        return NoContent();
    }

    /// <summary>Supprime un morceau, ses versions et leurs fichiers.</summary>
    /// <param name="id">Identifiant du morceau.</param>
    /// <response code="204">Morceau supprimé.</response>
    /// <response code="404">Morceau introuvable.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, [FromServices] DeleteSongHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(id, ct);
        return NoContent();
    }
}