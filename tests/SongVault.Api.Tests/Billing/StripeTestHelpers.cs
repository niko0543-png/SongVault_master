using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using SongVault.Application.Abstractions;
using SongVault.Domain.Billing;

namespace SongVault.Api.Tests.Billing;

/// <summary>Stripe dans les tests : faux Stripe, événements signés comme Stripe les signe.</summary>
public static class StripeTestHelpers
{
    /// <summary>Secret du webhook dans les tests (Billing:Stripe:WebhookSecret de SongVaultApiFactory).</summary>
    public const string WebhookSecret = "whsec_test_songvault";

    /// <summary>Hôte dérivé dont Stripe est remplacé par ce faux.</summary>
    public static WebApplicationFactory<Program> WithPayments(this SongVaultApiFactory factory, TestPaymentGateway payments)
        => factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IPaymentGateway>();
            s.AddSingleton<IPaymentGateway>(payments);
        }));

    /// <summary>Envoie un événement au webhook, signé comme Stripe le signe (ou avec un autre secret).</summary>
    public static Task<HttpResponseMessage> PostStripeEventAsync(
        this HttpClient client, string payload, string secret = WebhookSecret, DateTimeOffset? signedAt = null)
    {
        var timestamp = (signedAt ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();
        var signature = Convert.ToHexStringLower(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{payload}")));
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/billing/stripe/webhook")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Stripe-Signature", $"t={timestamp},v1={signature}");
        return client.SendAsync(request);
    }

    /// <summary>Un événement Stripe réduit à ce que l'API lit.</summary>
    public static string StripeEvent(string id, string type, string subscriptionId, Guid? bandId = null)
    {
        object data = type switch
        {
            "checkout.session.completed" => new
            {
                id = "cs_test_1",
                @object = "checkout.session",
                subscription = subscriptionId,
                client_reference_id = bandId?.ToString(),
            },
            "invoice.payment_failed" => new
            {
                id = "in_test_1",
                @object = "invoice",
                parent = new { subscription_details = new { subscription = subscriptionId } },
            },
            _ => new { id = subscriptionId, @object = "subscription", metadata = new { band_id = bandId?.ToString() } },
        };
        return JsonSerializer.Serialize(new { id, type, data = new { @object = data } });
    }
}

/// <summary>Stripe en mémoire pour les tests d'API : States décrit ce que Stripe répondrait.</summary>
public sealed class TestPaymentGateway : IPaymentGateway
{
    public ConcurrentDictionary<string, SubscriptionState> States { get; } = new();
    public ConcurrentQueue<CheckoutRequest> Checkouts { get; } = new();

    public bool IsEnabled => true;

    public Task<Uri> CreateCheckoutAsync(CheckoutRequest request, CancellationToken ct)
    {
        Checkouts.Enqueue(request);
        return Task.FromResult(new Uri("https://checkout.stripe.test/c/1"));
    }

    public Task<Uri> CreatePortalAsync(string customerId, Uri returnUrl, CancellationToken ct)
        => Task.FromResult(new Uri($"https://billing.stripe.test/p/{customerId}"));

    public Task<SubscriptionState?> GetSubscriptionAsync(string subscriptionId, CancellationToken ct)
        => Task.FromResult(States.TryGetValue(subscriptionId, out var state) ? state : null);

    public Task CancelSubscriptionAsync(string subscriptionId, CancellationToken ct) => Task.CompletedTask;
}