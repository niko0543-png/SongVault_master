using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

using SongVault.Api.Contracts.Notifications;
using SongVault.Api.Security;
using SongVault.Application.Notifications;

namespace SongVault.Api.Controllers;

/// <summary>Notifications par e-mail (ADR 0018) : vos préférences, et le lien de désinscription des e-mails.</summary>
[ApiController]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public sealed class NotificationsController : ControllerBase
{
    /// <summary>Un mode par type d'e-mail : Off, Immediate ou Daily (résumé du matin).</summary>
    [HttpGet("api/me/notifications")]
    [ProducesResponseType<IReadOnlyList<NotificationPreferenceResponse>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<NotificationPreferenceResponse>> Get(
        [FromServices] GetNotificationPreferencesHandler handler, CancellationToken ct)
        => [.. (await handler.HandleAsync(ct)).Select(NotificationPreferenceResponse.From)];

    /// <summary>Change le mode des types donnés ; les autres gardent le leur. Valable dans tous vos groupes.</summary>
    /// <response code="200">Toutes vos préférences, après modification.</response>
    /// <response code="400">Type ou mode manquant ou inconnu, ou type donné deux fois.</response>
    [HttpPut("api/me/notifications")]
    [ProducesResponseType<IReadOnlyList<NotificationPreferenceResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(
        IReadOnlyList<NotificationPreferenceRequest> request, [FromServices] UpdateNotificationPreferencesHandler handler,
        CancellationToken ct)
    {
        if (request.GroupBy(p => p.Kind).Any(g => g.Count() > 1))
        {
            ModelState.AddModelError("kind", "Chaque type ne peut apparaître qu'une fois.");
            return ValidationProblem(ModelState);
        }

        var saved = await handler.HandleAsync(request.ToDictionary(p => p.Kind!.Value, p => p.Mode!.Value), ct);
        return Ok(saved.Select(NotificationPreferenceResponse.From).ToList());
    }

    /// <summary>Lien de désinscription d'un e-mail : coupe ce type d'e-mail, ou tous pour le résumé.</summary>
    /// <remarks>
    /// Sans connexion : le jeton signé suffit. Appelé par la page /unsubscribe du front, et directement par la
    /// messagerie pour la désinscription en un clic (en-tête List-Unsubscribe-Post, RFC 8058), dont le corps est ignoré.
    /// Recommencer ne change rien.
    /// </remarks>
    /// <response code="200">Désinscription faite.</response>
    /// <response code="400">Jeton absent ou invalide.</response>
    [HttpPost("api/notifications/unsubscribe"), AllowAnonymous]
    [EnableRateLimiting(RateLimiting.Auth)]
    [ProducesResponseType<UnsubscribeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Unsubscribe(
        [FromQuery] string? token, [FromServices] UnsubscribeHandler handler, CancellationToken ct)
    {
        var result = string.IsNullOrWhiteSpace(token) ? null : await handler.HandleAsync(token, ct);
        return result is null
            ? Problem(statusCode: StatusCodes.Status400BadRequest, title: "Lien de désinscription invalide",
                detail: "Ce lien est incomplet ou a été modifié. Choisissez vos notifications dans « Mon compte ».")
            : Ok(new UnsubscribeResponse(result.Kind));
    }
}