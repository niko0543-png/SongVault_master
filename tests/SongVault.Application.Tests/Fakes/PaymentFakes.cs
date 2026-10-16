using SongVault.Application.Abstractions;
using SongVault.Application.Common.Exceptions;
using SongVault.Domain.Billing;

namespace SongVault.Application.Tests.Fakes;

/// <summary>Stripe en mémoire : Subscriptions décrit ce que Stripe répondrait, Checkouts ce qui a été demandé.</summary>
internal sealed class FakePaymentGateway(bool enabled = true) : IPaymentGateway
{
    public Dictionary<string, SubscriptionState> Subscriptions { get; } = [];
    public List<CheckoutRequest> Checkouts { get; } = [];
    public List<string> Canceled { get; } = [];

    public bool IsEnabled => enabled;

    public Task<Uri> CreateCheckoutAsync(CheckoutRequest request, CancellationToken ct)
    {
        if (!enabled) throw new BillingUnavailableException("Fermé.");
        Checkouts.Add(request);
        return Task.FromResult(new Uri("https://checkout.stripe.test/c/1"));
    }

    public Task<Uri> CreatePortalAsync(string customerId, Uri returnUrl, CancellationToken ct)
        => Task.FromResult(new Uri($"https://billing.stripe.test/p/{customerId}"));

    public Task<SubscriptionState?> GetSubscriptionAsync(string subscriptionId, CancellationToken ct)
        => Task.FromResult(Subscriptions.TryGetValue(subscriptionId, out var state) ? state : null);

    public Task CancelSubscriptionAsync(string subscriptionId, CancellationToken ct)
    {
        Canceled.Add(subscriptionId);
        return Task.CompletedTask;
    }
}