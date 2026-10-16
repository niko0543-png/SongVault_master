using System.Text;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SongVault.Application.Abstractions;
using SongVault.Application.Billing;

namespace SongVault.Api.Controllers;

/// <summary>
/// Adresse appelée par Stripe à chaque événement d'abonnement (ADR 0023). Sans cookie : la signature
/// Stripe-Signature, vérifiée sur le corps exact reçu, prouve que l'appel vient de Stripe.
/// </summary>
[ApiController]
[Route("api/billing/stripe/webhook")]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]                   // appelé par Stripe seulement
public sealed class StripeWebhookController(ILogger<StripeWebhookController> logger) : ControllerBase
{
    /// <summary>Événements Stripe : quelques Ko ; au-delà, ce n'est pas Stripe.</summary>
    private const long MaxPayloadBytes = 512 * 1024;

    /// <summary>200 : reçu (traité, déjà traité ou ignoré). 400 : signature absente ou fausse. Autre : Stripe renverra.</summary>
    [HttpPost]
    [RequestSizeLimit(MaxPayloadBytes)]
    public async Task<IActionResult> Receive(
        [FromServices] IPaymentEventReader reader, [FromServices] HandlePaymentEventHandler handler, CancellationToken ct)
    {
        // Le corps brut, tel quel : la signature porte sur ces octets, pas sur un objet relu
        using var body = new StreamReader(Request.Body, Encoding.UTF8);
        var payload = await body.ReadToEndAsync(ct);

        var paymentEvent = reader.Read(payload, Request.Headers["Stripe-Signature"]);
        var outcome = await handler.HandleAsync(paymentEvent, ct);
        logger.LogInformation("Événement Stripe {EventId} ({Type}) : {Outcome}", paymentEvent.Id, paymentEvent.Type, outcome);
        return Ok();
    }
}