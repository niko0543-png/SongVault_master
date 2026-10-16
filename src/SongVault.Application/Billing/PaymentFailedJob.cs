using System.Net;

using Microsoft.Extensions.Options;

using SongVault.Application.Abstractions;
using SongVault.Application.Common;

namespace SongVault.Application.Billing;

/// <summary>
/// Prévient les Owner d'un groupe que le paiement de l'abonnement a échoué (payload : identifiant du groupe).
/// Stripe réessaie la carte quelques jours ; la formule reste en place pendant ce temps (statut PastDue).
/// </summary>
public sealed class PaymentFailedJob(IBillingRepository billing, IEmailSender emailSender, IOptions<AppOptions> app)
    : IBackgroundJobHandler
{
    public const string JobType = "billing.payment-failed";

    public string Type => JobType;

    public async Task RunAsync(BackgroundJobContext job, CancellationToken ct)
    {
        var bandId = Guid.Parse(job.Payload);
        if (await billing.GetBandOwnersAsync(bandId, ct) is not { } owners) return;     // groupe effacé depuis
        var link = StartCheckoutHandler.BillingPage(app.Value.PublicUrl, bandId);
        foreach (var email in owners.Emails)
            await emailSender.SendAsync(BillingEmails.PaymentFailed(email, owners.BandName, link), ct);
    }
}

public static class BillingEmails
{
    public static EmailMessage PaymentFailed(string to, string bandName, string link)
    {
        var subject = $"Paiement refusé pour « {bandName} » sur SongVault";
        var text = $"""
            Le dernier paiement de l'abonnement du groupe « {bandName} » a été refusé.

            Stripe réessaie la carte pendant quelques jours ; la formule reste en place pendant ce temps.
            Pour mettre à jour la carte : {link}, puis « Gérer l'abonnement ».

            Sans paiement, le groupe repassera sur la formule gratuite : rien ne sera effacé, mais les nouveaux envois
            seront refusés au-delà du quota gratuit.
            """;

        Func<string, string> h = s => WebUtility.HtmlEncode(s);
        var html = $"""
            <p>Le dernier paiement de l'abonnement du groupe « {h(bandName)} » a été refusé.</p>
            <p>Stripe réessaie la carte pendant quelques jours ; la formule reste en place pendant ce temps.</p>
            <p><a href="{h(link)}">Mettre à jour la carte</a> (page Facturation, puis « Gérer l'abonnement »).</p>
            <p style="color:#666">Sans paiement, le groupe repassera sur la formule gratuite : rien ne sera effacé, mais les nouveaux envois seront refusés au-delà du quota gratuit.</p>
            """;

        return new EmailMessage(to, subject, html, text);
    }
}