using Microsoft.AspNetCore.Mvc;

using SongVault.Api.Contracts.Me;
using SongVault.Application.Accounts;

namespace SongVault.Api.Controllers;

/// <summary>Le compte connecté : export de ses données (ADR 0011).</summary>
[ApiController]
[Route("api/me")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public sealed class MeController : ControllerBase
{
    /// <summary>Demande une archive de vos données ; un e-mail prévient quand elle est prête.</summary>
    /// <response code="202">Archive en préparation.</response>
    /// <response code="409">Une archive est déjà en préparation.</response>
    [HttpPost("export")]
    [ProducesResponseType<DataExportResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RequestExport([FromServices] RequestDataExportHandler handler, CancellationToken ct)
        => Accepted(DataExportResponse.From(await handler.HandleAsync(ct)));

    /// <summary>État de la dernière archive.</summary>
    /// <response code="200">La dernière archive : en préparation, prête ou en échec.</response>
    /// <response code="204">Aucune archive, ou la dernière a expiré.</response>
    [HttpGet("export")]
    [ProducesResponseType<DataExportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LatestExport([FromServices] GetLatestDataExportHandler handler, CancellationToken ct)
        => await handler.HandleAsync(ct) is { } export ? Ok(DataExportResponse.From(export)) : NoContent();

    /// <summary>Télécharge la dernière archive, si elle est prête.</summary>
    /// <response code="200">Le fichier ZIP.</response>
    /// <response code="404">Pas d'archive prête.</response>
    [HttpGet("export/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadExport([FromServices] DownloadDataExportHandler handler, CancellationToken ct)
    {
        var file = await handler.HandleAsync(ct);
        return File(file.Content, file.ContentType, file.FileName);
    }
}