using Microsoft.AspNetCore.Mvc;

using SongVault.Api.Auth;
using SongVault.Api.Contracts.Songs;
using SongVault.Application.Songs.Rights;

namespace SongVault.Api.Controllers;

/// <summary>Crédits, répartition des droits et codes ISWC/ISRC d'un morceau (ADR 0029).</summary>
[ApiController]
[Route("api/bands/{bandId:guid}/songs/{songId:guid}")]
[BandScoped]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
public sealed class SongRightsController : ControllerBase
{
    /// <summary>ISWC, crédits et leur total, ISRC de chaque version en place.</summary>
    /// <response code="200">Les droits du morceau (éventuellement aucun crédit).</response>
    [HttpGet("rights")]
    [ProducesResponseType<SongRightsResponse>(StatusCodes.Status200OK)]
    public async Task<SongRightsResponse> Get(Guid songId, [FromServices] GetSongRightsHandler handler, CancellationToken ct)
        => SongRightsResponse.From(await handler.HandleAsync(songId, ct));

    /// <summary>Remplace l'ISWC et tous les crédits.</summary>
    /// <response code="200">Droits enregistrés.</response>
    /// <response code="400">Nom, rôle ou part manquant, nom trop long, plus de 30 crédits.</response>
    /// <response code="409">Le morceau a été modifié entre-temps ; rechargez-le.</response>
    /// <response code="422">Total différent de 100 %, part invalide, doublon, ISWC invalide.</response>
    [HttpPut("rights")]
    [ProducesResponseType<SongRightsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<SongRightsResponse> Set(
        Guid songId, SetSongRightsRequest request, [FromServices] SetSongRightsHandler handler, CancellationToken ct)
        => SongRightsResponse.From(await handler.HandleAsync(request.ToCommand(songId), ct));

    /// <summary>Pose ou retire l'ISRC d'une version.</summary>
    /// <response code="200">ISRC enregistré, en forme lisible.</response>
    /// <response code="422">ISRC invalide.</response>
    [HttpPut("versions/{versionId:guid}/isrc")]
    [ProducesResponseType<VersionIsrcResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<VersionIsrcResponse> SetIsrc(
        Guid songId, Guid versionId, SetVersionIsrcRequest request, [FromServices] SetVersionIsrcHandler handler, CancellationToken ct)
        => VersionIsrcResponse.From(await handler.HandleAsync(new SetVersionIsrcCommand(songId, versionId, request.Isrc), ct));

    /// <summary>Crédits du morceau en CSV pour Excel : point-virgule, UTF-8 avec BOM, parts à virgule.</summary>
    /// <response code="200">Le fichier credits-Titre.csv.</response>
    [HttpGet("rights.csv")]
    [Produces("text/csv")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Export(Guid songId, [FromServices] ExportSongCreditsHandler handler, CancellationToken ct)
        => this.ToResult(await handler.HandleAsync(songId, ct), asAttachment: true, enableRangeProcessing: false);
}