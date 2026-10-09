using Microsoft.AspNetCore.Mvc;

using SongVault.Api.Auth;
using SongVault.Api.Contracts.Bands;
using SongVault.Application.Bands;

namespace SongVault.Api.Controllers;

/// <summary>Groupes de l'utilisateur connecté.</summary>
[ApiController]
[Route("api/bands")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public sealed class BandsController : ControllerBase
{
    /// <summary>Mes groupes, triés par nom. Crée le groupe personnel au premier appel.</summary>
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

    /// <summary>Renomme le groupe.</summary>
    /// <response code="204">Groupe renommé.</response>
    [HttpPut("{bandId:guid}"), BandScoped]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Rename(
        Guid bandId, RenameBandRequest request, [FromServices] RenameBandHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(request.Name, ct);
        return NoContent();
    }
}