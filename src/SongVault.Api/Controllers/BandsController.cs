using Microsoft.AspNetCore.Mvc;

using SongVault.Api.Auth;
using SongVault.Api.Contracts.Bands;
using SongVault.Application.Bands;
using SongVault.Domain.Bands;

namespace SongVault.Api.Controllers;

/// <summary>Groupes de l'utilisateur connecté.</summary>
[ApiController]
[Route("api/bands")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public sealed class BandsController : ControllerBase
{
    /// <summary>Mes groupes, triés par nom. Liste vide pour un nouvel inscrit : il crée son premier groupe par POST.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<BandResponse>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<BandResponse>> Mine([FromServices] ListMyBandsHandler handler, CancellationToken ct)
        => [.. (await handler.HandleAsync(ct)).Select(BandResponse.From)];

    /// <summary>Détail d'un groupe dont je suis membre.</summary>
    /// <response code="404">Groupe inexistant ou dont vous n'êtes pas membre.</response>
    [HttpGet("{bandId:guid}"), BandScoped]
    [ProducesResponseType<BandResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BandResponse>> GetById(
        Guid bandId, [FromServices] ListMyBandsHandler handler, CancellationToken ct)
        => BandResponse.From((await handler.HandleAsync(ct)).Single(b => b.Id == bandId));

    /// <summary>Crée un groupe dont je deviens Owner.</summary>
    /// <response code="201">Groupe créé ; l'en-tête Location pointe vers sa ressource.</response>
    [HttpPost]
    [ProducesResponseType<BandResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BandResponse>> Create(
        CreateBandRequest request, [FromServices] CreateBandHandler handler, CancellationToken ct)
    {
        var band = await handler.HandleAsync(request.Name, ct);
        return CreatedAtAction(nameof(GetById), new { bandId = band.Id }, BandResponse.From(band));
    }

    /// <summary>Renomme le groupe. Réservé aux Owner.</summary>
    /// <response code="204">Groupe renommé.</response>
    /// <response code="403">Vous n'êtes pas Owner du groupe.</response>
    [HttpPut("{bandId:guid}"), BandScoped, MinimumBandRole(BandRole.Owner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Rename(
        Guid bandId, RenameBandRequest request, [FromServices] RenameBandHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(request.Name, ct);
        return NoContent();
    }

    /// <summary>
    /// Supprime définitivement le groupe et tout son contenu : morceaux (corbeille comprise), fichiers, sorties,
    /// setlists, invitations, liens de partage, journal. Réservé aux Owner ; le nom du groupe est recopié pour confirmer.
    /// </summary>
    /// <response code="204">Groupe supprimé ; ses membres n'y ont plus accès.</response>
    /// <response code="403">Vous n'êtes pas Owner du groupe.</response>
    /// <response code="409">Le groupe a un abonnement en cours : à résilier d'abord depuis la page Facturation.</response>
    /// <response code="422">Le nom recopié ne correspond pas à celui du groupe.</response>
    [HttpDelete("{bandId:guid}"), BandScoped, MinimumBandRole(BandRole.Owner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(
        Guid bandId, DeleteBandRequest request, [FromServices] DeleteBandHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(request.ConfirmName, ct);
        return NoContent();
    }
}