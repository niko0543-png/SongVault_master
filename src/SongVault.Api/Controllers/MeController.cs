using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

using SongVault.Api.Contracts.Me;
using SongVault.Api.Localization;
using SongVault.Application.Common.Exceptions;
using SongVault.Api.Security;
using SongVault.Application.Accounts;

using static SongVault.Api.Contracts.Me.DataExportResponse;

namespace SongVault.Api.Controllers;

/// <summary>Le compte connecté : export de ses données et suppression (ADR 0011).</summary>
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
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadExport([FromServices] DownloadDataExportHandler handler, CancellationToken ct)
    {
        var file = await handler.HandleAsync(ct);
        return this.ToResult(file, asAttachment: true, enableRangeProcessing: false);
    }


    /// <summary>Enregistre la langue de l'interface sur le compte (#26) ; elle suit l'utilisateur sur ses appareils.</summary>
    /// <response code="204">Langue enregistrée.</response>
    /// <response code="400">Langue non prise en charge (fr ou en).</response>
    [HttpPut("locale")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SaveLocale(LocaleRequest request, [FromServices] UserManager<IdentityUser> users)
    {
        if (!AccountLocale.Supported.Contains(request.Locale))
        {
            ModelState.AddModelError(nameof(request.Locale), "Langue non prise en charge : choisissez fr ou en.");
            return ValidationProblem(ModelState);
        }
        var user = await users.GetUserAsync(User) ?? throw new NotFoundException("Account", "connecté");
        await AccountLocale.SetAsync(users, user, request.Locale!);
        return NoContent();
    }

    /// <summary>Supprime votre compte : désactivé tout de suite, effacé définitivement 30 jours plus tard.</summary>
    /// <remarks>
    /// Vous quittez vos groupes partagés ; ceux dont vous êtes le seul membre seront effacés avec le compte.
    /// Le mot de passe est redemandé. Même limite par IP que la connexion.
    /// </remarks>
    /// <response code="204">Compte désactivé ; cette session est fermée, les autres le seront dans la minute.</response>
    /// <response code="409">Vous êtes le dernier Owner d'un groupe qui a d'autres membres.</response>
    /// <response code="422">Mot de passe incorrect.</response>
    [HttpPost("delete")]
    [EnableRateLimiting(RateLimiting.Auth)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> DeleteAccount(
        DeleteAccountRequest request, [FromServices] DeleteAccountHandler handler,
        [FromServices] SignInManager<IdentityUser> signInManager, CancellationToken ct)
    {
        await handler.HandleAsync(new DeleteAccountCommand(request.Password!), ct);
        await signInManager.SignOutAsync();
        return NoContent();
    }
}