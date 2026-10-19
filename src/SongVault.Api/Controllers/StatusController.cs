using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

using SongVault.Api.Contracts.ServiceStatus;
using SongVault.Api.Security;
using SongVault.Api.ServiceStatus;

namespace SongVault.Api.Controllers;

/// <summary>
/// État du service pour la page /statut et la vérification de disponibilité (#18). Ouvert à tous.
/// </summary>
[ApiController]
[Route("api/status")]
[AllowAnonymous]
[EnableRateLimiting(RateLimiting.Status)]
[ProducesResponseType(StatusCodes.Status429TooManyRequests)]
public sealed class StatusController : ControllerBase
{
    /// <summary>État de l'application, de la base de données et des tâches de fond, vérifié il y a 30 s au plus.</summary>
    /// <response code="200">Service en état de marche ("ok") ou ralenti ("degraded").</response>
    /// <response code="503">Service en panne ("down") : la base de données ne répond pas.</response>
    [HttpGet]
    [ProducesResponseType<StatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<StatusResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get([FromServices] StatusReporter reporter, CancellationToken ct)
    {
        var status = await reporter.GetAsync(ct);
        Response.Headers.CacheControl = "no-store";
        return status.Status == "down"
            ? StatusCode(StatusCodes.Status503ServiceUnavailable, status)
            : Ok(status);
    }
}