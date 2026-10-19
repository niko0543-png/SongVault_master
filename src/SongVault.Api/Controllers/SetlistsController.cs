using Microsoft.AspNetCore.Mvc;

using SongVault.Api.Contracts.Setlists;
using SongVault.Api.Auth;
using SongVault.Application.Setlists;
using SongVault.Domain.Setlists;

namespace SongVault.Api.Controllers;

/// <summary>Setlists du groupe et leurs morceaux dans l'ordre de jeu (ADR 0028).</summary>
[ApiController]
[Route("api/bands/{bandId:guid}/setlists")]
[BandScoped]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
public sealed class SetlistsController : ControllerBase
{
    /// <summary>Setlists du groupe : de travail (sans date) d'abord, puis de la plus récente à la plus ancienne.</summary>
    /// <response code="200">Les setlists (éventuellement aucune).</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<SetlistSummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<SetlistSummaryResponse>> List(
        [FromServices] ListSetlistsHandler handler, CancellationToken ct)
        => [.. (await handler.HandleAsync(ct)).Select(SetlistSummaryResponse.From)];

    /// <summary>Détail d'une setlist : lignes dans l'ordre, tonalité, BPM, durées et durée totale.</summary>
    /// <response code="200">La setlist.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<SetlistResponse>(StatusCodes.Status200OK)]
    public async Task<SetlistResponse> GetById(Guid id, [FromServices] GetSetlistHandler handler, CancellationToken ct)
        => SetlistResponse.From(await handler.HandleAsync(id, ct));

    /// <summary>Crée une setlist vide.</summary>
    /// <response code="201">Setlist créée ; Location pointe vers son détail.</response>
    /// <response code="400">Nom manquant ou trop long, lieu trop long.</response>
    [HttpPost]
    [ProducesResponseType<SetlistResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SetlistResponse>> Create(
        Guid bandId, SaveSetlistRequest request, [FromServices] CreateSetlistHandler handler, CancellationToken ct)
    {
        var setlist = await handler.HandleAsync(new CreateSetlistCommand(request.Name, request.Date, request.Venue), ct);
        return CreatedAtAction(nameof(GetById), new { bandId, id = setlist.Id }, SetlistResponse.From(setlist));
    }

    /// <summary>Modifie le nom, la date et le lieu.</summary>
    /// <response code="204">Setlist modifiée.</response>
    /// <response code="409">La setlist a été modifiée entre-temps ; rechargez-la.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        Guid id, SaveSetlistRequest request, [FromServices] UpdateSetlistHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(new UpdateSetlistCommand(id, request.Name, request.Date, request.Venue), ct);
        return NoContent();
    }

    /// <summary>Supprime la setlist et ses lignes. Les morceaux et leurs versions restent.</summary>
    /// <response code="204">Setlist supprimée.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, [FromServices] DeleteSetlistHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(id, ct);
        return NoContent();
    }

    /// <summary>Remplace toutes les lignes par la liste envoyée, dans son ordre (ajouter, retirer, réordonner, annoter).</summary>
    /// <remarks>Un morceau figure au plus une fois ; une version donnée doit appartenir à son morceau, dans le groupe.</remarks>
    /// <response code="200">La setlist avec ses nouvelles lignes et sa durée totale.</response>
    /// <response code="409">Lignes modifiées entre-temps par quelqu'un d'autre ; rechargez la setlist.</response>
    /// <response code="422">Morceau en double ou hors du groupe, version d'un autre morceau, ou plus de 50 lignes.</response>
    [HttpPut("{id:guid}/items")]
    [ProducesResponseType<SetlistResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<SetlistResponse> SetItems(
        Guid id, SetSetlistItemsRequest request, [FromServices] SetSetlistItemsHandler handler, CancellationToken ct)
    {
        var items = request.Items
            .Select(i => new SetlistItemChoice(i.SongId!.Value, i.VersionId, i.Notes, i.DurationMs))
            .ToList();
        return SetlistResponse.From(await handler.HandleAsync(new SetSetlistItemsCommand(id, items), ct));
    }
}