using Microsoft.AspNetCore.Mvc;

using SongVault.Api.Auth;
using SongVault.Api.Contracts.Trash;
using SongVault.Application.Trash;
using SongVault.Domain.Bands;
using SongVault.Domain.Trash;

namespace SongVault.Api.Controllers;

/// <summary>
/// Corbeille du groupe (#20, ADR 0027) : morceaux, versions et fichiers supprimés depuis moins de 30 jours.
/// Les Member restaurent ; seuls les Owner suppriment définitivement avant la purge.
/// </summary>
[ApiController]
[Route("api/bands/{bandId:guid}/trash")]
[BandScoped, MinimumBandRole(BandRole.Member)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
public sealed class TrashController : ControllerBase
{
    /// <summary>Le contenu de la corbeille, le plus récent d'abord.</summary>
    /// <response code="200">La corbeille (éventuellement vide).</response>
    /// <response code="403">Un Guest ne voit pas la corbeille.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TrashItemResponse>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<TrashItemResponse>> List([FromServices] ListTrashHandler handler, CancellationToken ct)
        => [.. (await handler.HandleAsync(ct)).Select(TrashItemResponse.From)];

    /// <summary>Remet un élément en place, avec exactement ce qui est parti avec lui.</summary>
    /// <param name="kind">song, version ou file.</param>
    /// <param name="itemId">Identifiant du morceau, de la version ou du fichier.</param>
    /// <response code="204">Restauré.</response>
    /// <response code="404">Pas dans la corbeille de ce groupe.</response>
    /// <response code="409">Ce qui le contient est aussi à la corbeille : le restaurer d'abord.</response>
    [HttpPost("{kind}/{itemId:guid}/restore")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Restore(
        TrashItemKind kind, Guid itemId, [FromServices] RestoreFromTrashHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(kind, itemId, ct);
        return NoContent();
    }

    /// <summary>Supprime définitivement un élément de la corbeille, fichiers compris, sans attendre les 30 jours.</summary>
    /// <param name="kind">song, version ou file.</param>
    /// <param name="itemId">Identifiant du morceau, de la version ou du fichier.</param>
    /// <response code="204">Supprimé ; la place est libérée.</response>
    /// <response code="403">Réservé aux Owner.</response>
    /// <response code="404">Pas dans la corbeille de ce groupe.</response>
    [HttpDelete("{kind}/{itemId:guid}"), MinimumBandRole(BandRole.Owner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(
        TrashItemKind kind, Guid itemId, [FromServices] DeleteFromTrashHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(kind, itemId, ct);
        return NoContent();
    }
}