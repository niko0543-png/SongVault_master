using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

using SongVault.Api.Contracts.Sharing;
using SongVault.Api.Security;
using SongVault.Application.Sharing;

namespace SongVault.Api.Controllers;

/// <summary>
/// Ce que voit une personne sans compte qui ouvre un lien de partage (ADR 0019). Lecture seule :
/// aucune route de ce contrôleur ne modifie un morceau, une version ou une sortie.
/// Jamais de 401 ici : le front se déconnecterait. Lien inconnu, expiré ou révoqué : 404 ; mot de passe attendu : 403.
/// </summary>
[ApiController]
[Route("api/share/{token}")]
[AllowAnonymous]
[EnableRateLimiting(RateLimiting.Share)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType(StatusCodes.Status429TooManyRequests)]
public sealed class SharedController : ControllerBase
{
    /// <summary>Cookie posé après le bon mot de passe, limité aux routes de ce lien.</summary>
    public const string UnlockCookie = "songvault.share";

    /// <summary>Groupe, titre et pistes avec leurs fichiers audio. Chaque ouverture entre au journal.</summary>
    /// <response code="403">Lien protégé : envoyer d'abord le mot de passe à /unlock.</response>
    [HttpGet]
    [ProducesResponseType<SharedContentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<SharedContentResponse> Open(string token, [FromServices] OpenSharedContentHandler handler, CancellationToken ct)
    {
        PrivateResponse();
        return SharedContentResponse.From(await handler.HandleAsync(token, UnlockToken(), Visitor(), ct));
    }

    /// <summary>Vérifie le mot de passe et pose le cookie de déverrouillage (12 h au plus).</summary>
    /// <response code="204">Mot de passe accepté.</response>
    /// <response code="422">Mot de passe incorrect.</response>
    [HttpPost("unlock")]
    [EnableRateLimiting(RateLimiting.Auth)]          // contre l'essai de mots de passe en série : 10 par minute et par IP
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Unlock(
        string token, UnlockShareRequest request, [FromServices] UnlockShareLinkHandler handler, CancellationToken ct)
    {
        var unlock = await handler.HandleAsync(token, request.Password, Visitor(), ct);
        Response.Cookies.Append(UnlockCookie, unlock.UnlockToken, new CookieOptions
        {
            Path = $"/api/share/{token}",            // envoyé aux seules routes de ce lien
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Expires = unlock.ExpiresAt,
            IsEssential = true,
        });
        return NoContent();
    }

    /// <summary>Fichier audio de la cible, pour le lecteur de la page. Le début de l'écoute entre au journal.</summary>
    /// <response code="302">Stockage objet : redirection vers une URL de lecture signée et courte.</response>
    /// <response code="403">Lien protégé, cookie absent ou expiré.</response>
    [HttpGet("files/{fileId:guid}/content")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status206PartialContent)]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Play(
        string token, Guid fileId, [FromServices] DownloadSharedFileHandler handler, CancellationToken ct)
    {
        PrivateResponse();
        // Une reprise au milieu du fichier (avance rapide, suite du chargement) n'est pas une nouvelle écoute
        var range = Request.GetTypedHeaders().Range;
        var countAsPlay = range is null || range.Ranges.FirstOrDefault()?.From == 0;
        var file = await handler.HandleAsync(token, UnlockToken(), fileId, countAsPlay, Visitor(), ct);
        return this.ToResult(file, asAttachment: false, enableRangeProcessing: true);
    }

    /// <summary>Pochette de la sortie partagée.</summary>
    [HttpGet("cover")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Cover(string token, [FromServices] DownloadSharedCoverHandler handler, CancellationToken ct)
    {
        PrivateResponse();
        return this.ToResult(await handler.HandleAsync(token, UnlockToken(), ct), asAttachment: false, enableRangeProcessing: false);
    }

    private string? UnlockToken() => Request.Cookies[UnlockCookie];

    private ShareVisitor Visitor()
        => new(HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());

    // Ni indexé par les moteurs de recherche, ni gardé en cache : une révocation vaut tout de suite
    private void PrivateResponse()
    {
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        Response.Headers.CacheControl = "no-store";
    }
}