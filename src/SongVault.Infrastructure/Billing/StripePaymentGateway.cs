using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SongVault.Application.Abstractions;
using SongVault.Application.Common.Exceptions;
using SongVault.Domain.Billing;

namespace SongVault.Infrastructure.Billing;

/// <summary>
/// Appels à l'API HTTP de Stripe (ADR 0023), sans SDK : quatre requêtes en formulaire, réponses en JSON.
/// Clé secrète en en-tête Authorization, version de l'API fixée par Stripe-Version.
/// </summary>
internal sealed class StripePaymentGateway(
    HttpClient http, IOptions<BillingOptions> options, ILogger<StripePaymentGateway> logger) : IPaymentGateway
{
    private StripeOptions Stripe => options.Value.Stripe;

    public bool IsEnabled => true;

    public async Task<Uri> CreateCheckoutAsync(CheckoutRequest request, CancellationToken ct)
    {
        if (!Stripe.Prices.TryGetValue(request.PlanId, out var price) || string.IsNullOrWhiteSpace(price))
            throw new BillingUnavailableException($"La formule « {request.PlanId} » n'a pas encore de prix : réessayez plus tard.");

        var band = request.BandId.ToString();
        var form = new List<KeyValuePair<string, string>>
        {
            new("mode", "subscription"),
            new("line_items[0][price]", price),
            new("line_items[0][quantity]", "1"),
            new("success_url", request.SuccessUrl.ToString()),
            new("cancel_url", request.CancelUrl.ToString()),
            new("client_reference_id", band),
            new("metadata[band_id]", band),
            new("subscription_data[metadata][band_id]", band),     // relu sur chaque événement d'abonnement
            new("locale", "fr"),
        };
        if (request.CustomerId is not null) form.Add(new("customer", request.CustomerId));
        else if (request.CustomerEmail is not null) form.Add(new("customer_email", request.CustomerEmail));
        if (Stripe.AutomaticTax)
        {
            form.Add(new("automatic_tax[enabled]", "true"));
            if (request.CustomerId is not null) form.Add(new("customer_update[address]", "auto"));
        }
        if (Stripe.RequireTermsConsent) form.Add(new("consent_collection[terms_of_service]", "required"));

        using var json = (await SendAsync(HttpMethod.Post, "v1/checkout/sessions", form, ct))!;
        return new Uri(json.RootElement.GetProperty("url").GetString()!);
    }

    public async Task<Uri> CreatePortalAsync(string customerId, Uri returnUrl, CancellationToken ct)
    {
        using var json = (await SendAsync(HttpMethod.Post, "v1/billing_portal/sessions",
            [new("customer", customerId), new("return_url", returnUrl.ToString()), new("locale", "fr")], ct))!;
        return new Uri(json.RootElement.GetProperty("url").GetString()!);
    }

    public async Task<SubscriptionState?> GetSubscriptionAsync(string subscriptionId, CancellationToken ct)
    {
        using var json = await SendAsync(HttpMethod.Get, $"v1/subscriptions/{Uri.EscapeDataString(subscriptionId)}", null, ct,
            allowNotFound: true);
        return json is null ? null : ReadSubscription(json.RootElement);
    }

    public async Task CancelSubscriptionAsync(string subscriptionId, CancellationToken ct)
    {
        var path = $"v1/subscriptions/{Uri.EscapeDataString(subscriptionId)}";
        try
        {
            using var _ = await SendAsync(HttpMethod.Delete, path, null, ct, allowNotFound: true);
        }
        catch (BillingUnavailableException)
        {
            // Stripe refuse d'arrêter un abonnement déjà arrêté : c'est ce qu'on voulait
            if ((await GetSubscriptionAsync(subscriptionId, ct))?.Status is null or SubscriptionStatus.Canceled
                or SubscriptionStatus.IncompleteExpired) return;
            throw;
        }
    }

    /// <summary>Lit un abonnement Stripe ; la formule est retrouvée par son prix (Billing:Stripe:Prices).</summary>
    internal SubscriptionState ReadSubscription(JsonElement subscription)
    {
        var item = subscription.GetProperty("items").GetProperty("data")[0];
        var priceId = item.GetProperty("price").GetProperty("id").GetString();
        var planId = Stripe.Prices.FirstOrDefault(p => p.Value == priceId).Key;

        // Version 2025-03-31 : la fin de période est sur l'élément ; avant, sur l'abonnement
        var periodEnd = Unix(item, "current_period_end") ?? Unix(subscription, "current_period_end");
        var bandId = subscription.TryGetProperty("metadata", out var metadata)
                     && metadata.TryGetProperty("band_id", out var band)
                     && Guid.TryParse(band.GetString(), out var id) ? id : (Guid?)null;

        return new SubscriptionState(
            subscription.GetProperty("id").GetString()!,
            subscription.GetProperty("customer").GetString()!,
            bandId, planId, ParseStatus(subscription.GetProperty("status").GetString()), periodEnd,
            subscription.TryGetProperty("cancel_at_period_end", out var cancel) && cancel.GetBoolean());
    }

    internal static SubscriptionStatus ParseStatus(string? status) => status switch
    {
        "active" => SubscriptionStatus.Active,
        "trialing" => SubscriptionStatus.Trialing,
        "past_due" => SubscriptionStatus.PastDue,
        "incomplete" => SubscriptionStatus.Incomplete,
        "incomplete_expired" => SubscriptionStatus.IncompleteExpired,
        "canceled" => SubscriptionStatus.Canceled,
        "unpaid" => SubscriptionStatus.Unpaid,
        "paused" => SubscriptionStatus.Paused,
        _ => throw new InvalidOperationException($"État d'abonnement Stripe inconnu : « {status} »."),
    };

    private static DateTimeOffset? Unix(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? DateTimeOffset.FromUnixTimeSeconds(value.GetInt64())
            : null;

    private async Task<JsonDocument?> SendAsync(HttpMethod method, string path,
        IEnumerable<KeyValuePair<string, string>>? form, CancellationToken ct, bool allowNotFound = false)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Stripe.SecretKey);
        request.Headers.Add("Stripe-Version", Stripe.ApiVersion);
        if (form is not null) request.Content = new FormUrlEncodedContent(form);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (Exception ex) when ((ex is HttpRequestException or TaskCanceledException) && !ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Stripe injoignable ({Method} {Path})", method, path);
            throw new BillingUnavailableException("Le service de paiement est injoignable. Réessayez dans un instant.", ex);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            if (allowNotFound && response.StatusCode == HttpStatusCode.NotFound) return null;
            if (!response.IsSuccessStatusCode)
            {
                // Le corps d'erreur de Stripe ne contient jamais la clé secrète : on peut le journaliser
                logger.LogError("Stripe a refusé {Method} {Path} ({Status}) : {Body}", method, path, (int)response.StatusCode, body);
                throw new BillingUnavailableException(
                    $"Le service de paiement a refusé la demande ({((int)response.StatusCode).ToString(CultureInfo.InvariantCulture)}).");
            }
            return JsonDocument.Parse(body);
        }
    }
}

/// <summary>Billing:Provider=None : formules et quotas s'appliquent, rien ne se paie en ligne.</summary>
internal sealed class DisabledPaymentGateway : IPaymentGateway
{
    private const string Closed = "Le paiement en ligne n'est pas encore ouvert.";

    public bool IsEnabled => false;

    public Task<Uri> CreateCheckoutAsync(CheckoutRequest request, CancellationToken ct) => throw new BillingUnavailableException(Closed);

    public Task<Uri> CreatePortalAsync(string customerId, Uri returnUrl, CancellationToken ct) => throw new BillingUnavailableException(Closed);

    public Task<SubscriptionState?> GetSubscriptionAsync(string subscriptionId, CancellationToken ct) => throw new BillingUnavailableException(Closed);

    public Task CancelSubscriptionAsync(string subscriptionId, CancellationToken ct) => throw new BillingUnavailableException(Closed);
}