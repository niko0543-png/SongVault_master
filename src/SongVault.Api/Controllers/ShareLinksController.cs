using Microsoft.AspNetCore.Mvc;

using SongVault.Api.Auth;
using SongVault.Api.Contracts.Sharing;
using SongVault.Application.Sharing;
using SongVault.Domain.Bands;
using SongVault.Domain.Sharing;

namespace SongVault.Api.Controllers;

/// <summary>Liens de partage externes du groupe : création, liste, révocation, journal (ADR 0019).</summary>
[ApiController]
[Route("api/bands/{bandId:guid}/share-links")]
[BandScoped, MinimumBandRole(BandRole.Member)]       // GET compris : le journal dit qui a écouté, et quand
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
public sealed class ShareLinksController : ControllerBase
{
    /// <summary>Liens d'une version (?versionId=) ou d'une sortie (?releaseId=), actifs ou non, les plus récents d'abord.</summary>
    /// <response code="400">Ni versionId ni releaseId, ou les deux.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ShareLinkResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? versionId, [FromQuery] Guid? releaseId, [FromServices] ListShareLinksHandler handler,
        CancellationToken ct)
    {
        if (versionId.HasValue == releaseId.HasValue)
        {
            ModelState.AddModelError("versionId", "Donnez versionId ou releaseId, pas les deux.");
            return ValidationProblem(ModelState);
        }

        var links = versionId is { } id
            ? await handler.HandleAsync(ShareTargetType.Version, id, ct)
            : await handler.HandleAsync(ShareTargetType.Release, releaseId!.Value, ct);
        return Ok(links.Select(ShareLinkResponse.From).ToList());
    }

    /// <summary>Crée un lien d'écoute seule ; renvoie son adresse complète, une seule fois.</summary>
    /// <response code="201">Lien créé.</response>
    /// <response code="404">Version ou sortie inconnue dans ce groupe.</response>
    [HttpPost]
    [ProducesResponseType<ShareLinkCreatedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ShareLinkCreatedResponse>> Create(
        CreateShareLinkRequest request, [FromServices] CreateShareLinkHandler handler, CancellationToken ct)
    {
        var created = await handler.HandleAsync(new CreateShareLinkCommand(
            request.TargetType!.Value, request.TargetId!.Value, request.Label,
            request.ExpiresInDays ?? ShareLink.DefaultLifetimeDays, request.Password), ct);
        return StatusCode(StatusCodes.Status201Created, ShareLinkCreatedResponse.From(created));
    }

    /// <summary>Coupe le lien tout de suite. Il reste dans la liste, avec son journal.</summary>
    /// <response code="204">Lien révoqué (ou déjà révoqué).</response>
    [HttpPost("{linkId:guid}/revoke")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Revoke(Guid linkId, [FromServices] RevokeShareLinkHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(linkId, ct);
        return NoContent();
    }

    /// <summary>Efface le lien et son journal.</summary>
    /// <response code="204">Lien effacé.</response>
    [HttpDelete("{linkId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid linkId, [FromServices] DeleteShareLinkHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(linkId, ct);
        return NoContent();
    }

    /// <summary>Journal du lien : ouvertures, écoutes et mauvais mots de passe, le plus récent d'abord (200 lignes au plus).</summary>
    [HttpGet("{linkId:guid}/accesses")]
    [ProducesResponseType<IReadOnlyList<ShareLinkAccessResponse>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<ShareLinkAccessResponse>> Accesses(
        Guid linkId, [FromServices] ListShareLinkAccessesHandler handler, CancellationToken ct)
        => [.. (await handler.HandleAsync(linkId, ct)).Select(ShareLinkAccessResponse.From)];
}