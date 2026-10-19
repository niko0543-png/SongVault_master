using Microsoft.AspNetCore.Mvc;

using SongVault.Api.Auth;
using SongVault.Api.Contracts.Files;
using SongVault.Application.Files.Stems;

namespace SongVault.Api.Controllers;

/// <summary>Pistes séparées d'une version, lues ensemble par le mélangeur du navigateur (ADR 0030).</summary>
[ApiController]
[Route("api/bands/{bandId:guid}/songs/{songId:guid}/versions/{versionId:guid}/stems")]
[BandScoped]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
public sealed class StemsController : ControllerBase
{
    /// <summary>Les pistes de la version, dans l'ordre du mélangeur.</summary>
    /// <response code="200">Les pistes (éventuellement aucune) ; chaque fichier s'écoute par …/files/{fileId}/listen.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<StemResponse>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<StemResponse>> List(
        Guid songId, Guid versionId, [FromServices] GetStemsHandler handler, CancellationToken ct)
        => [.. (await handler.HandleAsync(songId, versionId, ct)).Select(StemResponse.From)];

    /// <summary>Remplace toutes les pistes de la version.</summary>
    /// <response code="200">Pistes enregistrées.</response>
    /// <response code="400">Fichier ou nom manquant, nom trop long, plus de 8 pistes.</response>
    /// <response code="409">La version a été modifiée entre-temps ; rechargez-la.</response>
    /// <response code="422">Fichier pas audio, d'une autre version ou à la corbeille, doublon, nom vide.</response>
    [HttpPut]
    [ProducesResponseType<IReadOnlyList<StemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IReadOnlyList<StemResponse>> Set(
        Guid songId, Guid versionId, SetStemsRequest request, [FromServices] SetStemsHandler handler, CancellationToken ct)
        => [.. (await handler.HandleAsync(request.ToCommand(songId, versionId), ct)).Select(StemResponse.From)];
}