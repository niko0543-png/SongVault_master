using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SongVault.Api.Auth;
using SongVault.Api.Contracts.Billing;
using SongVault.Application.Billing;
using SongVault.Domain.Bands;

namespace SongVault.Api.Controllers;

/// <summary>Formules, stockage et abonnement du groupe (ADR 0023).</summary>
[ApiController]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public sealed class BillingController : ControllerBase
{
    /// <summary>Les formules et leurs limites. Sans connexion : c'est la page Formules.</summary>
    [HttpGet("api/plans"), AllowAnonymous]
    [ProducesResponseType<PlanCatalogResponse>(StatusCodes.Status200OK)]
    public async Task<PlanCatalogResponse> Plans([FromServices] ListPlansHandler handler, CancellationToken ct)
        => PlanCatalogResponse.From(await handler.HandleAsync(ct));

    /// <summary>Stockage utilisé par le groupe et quota de sa formule. Lisible par tout membre, Guest compris.</summary>
    [HttpGet("api/bands/{bandId:guid}/usage"), BandScoped]
    [ProducesResponseType<UsageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<UsageResponse> Usage(Guid bandId, [FromServices] GetBandUsageHandler handler, CancellationToken ct)
        => UsageResponse.From(await handler.HandleAsync(ct));

    /// <summary>Formule, abonnement et actions possibles. Réservé aux Owner.</summary>
    /// <response code="403">Vous n'êtes pas Owner du groupe.</response>
    [HttpGet("api/bands/{bandId:guid}/billing"), BandScoped, MinimumBandRole(BandRole.Owner)]
    [ProducesResponseType<BillingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<BillingResponse> Get(Guid bandId, [FromServices] GetBillingHandler handler, CancellationToken ct)
        => BillingResponse.From(await handler.HandleAsync(ct));

    /// <summary>Commence le paiement d'une formule : renvoie la page Stripe Checkout où envoyer le navigateur.</summary>
    /// <remarks>La formule ne change qu'à la réception de l'événement Stripe (webhook), pas au retour sur le site.</remarks>
    /// <response code="403">Vous n'êtes pas Owner du groupe.</response>
    /// <response code="404">Formule inconnue.</response>
    /// <response code="409">Un abonnement court déjà : on en change par le portail.</response>
    /// <response code="422">Formule gratuite.</response>
    /// <response code="503">Paiement en ligne pas encore ouvert, ou Stripe injoignable.</response>
    [HttpPost("api/bands/{bandId:guid}/billing/checkout"), BandScoped, MinimumBandRole(BandRole.Owner)]
    [ProducesResponseType<RedirectResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<RedirectResponse> Checkout(
        Guid bandId, StartCheckoutRequest request, [FromServices] StartCheckoutHandler handler, CancellationToken ct)
        => new(await handler.HandleAsync(request.PlanId, ct));

    /// <summary>Ouvre le portail client Stripe : carte, factures, changement de formule, résiliation.</summary>
    /// <response code="403">Vous n'êtes pas Owner du groupe.</response>
    /// <response code="409">Aucun paiement n'a encore eu lieu pour ce groupe.</response>
    /// <response code="503">Paiement en ligne pas encore ouvert, ou Stripe injoignable.</response>
    [HttpPost("api/bands/{bandId:guid}/billing/portal"), BandScoped, MinimumBandRole(BandRole.Owner)]
    [ProducesResponseType<RedirectResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<RedirectResponse> Portal(Guid bandId, [FromServices] OpenBillingPortalHandler handler, CancellationToken ct)
        => new(await handler.HandleAsync(ct));
}