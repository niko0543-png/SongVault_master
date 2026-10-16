using SongVault.Application.Abstractions;
using SongVault.Domain.Billing;

namespace SongVault.Application.Billing;

public enum PaymentEventOutcome { Applied, Duplicate, Ignored }

/// <summary>
/// Un événement du webhook Stripe (ADR 0023). L'abonnement est toujours relu chez Stripe : l'ordre d'arrivée
/// des événements ne compte pas, seul l'état actuel est enregistré. Un événement déjà traité ne fait rien.
/// Une exception répond une erreur à Stripe, qui renvoie l'événement plus tard.
/// </summary>
public sealed class HandlePaymentEventHandler(
    IBillingRepository billing, IPaymentGateway payments, IBackgroundJobs jobs, IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public const string CheckoutCompleted = "checkout.session.completed";
    public const string PaymentFailed = "invoice.payment_failed";

    /// <summary>Événements à cocher dans Stripe pour l'adresse du webhook ; les autres sont ignorés.</summary>
    public static readonly IReadOnlySet<string> HandledTypes = new HashSet<string>
    {
        CheckoutCompleted,
        "customer.subscription.created",
        "customer.subscription.updated",
        "customer.subscription.deleted",
        PaymentFailed,
    };

    public async Task<PaymentEventOutcome> HandleAsync(PaymentEvent paymentEvent, CancellationToken ct)
    {
        if (!HandledTypes.Contains(paymentEvent.Type) || paymentEvent.SubscriptionId is null)
            return PaymentEventOutcome.Ignored;                    // ex. paiement unique, autre type coché par erreur
        if (await billing.IsEventProcessedAsync(paymentEvent.Id, ct)) return PaymentEventOutcome.Duplicate;

        var state = await payments.GetSubscriptionAsync(paymentEvent.SubscriptionId, ct);
        var bandId = state?.BandId ?? paymentEvent.BandId;
        if (state is null || bandId is not { } id || !await billing.BandExistsAsync(id, ct))
            return PaymentEventOutcome.Ignored;                    // abonnement effacé chez Stripe, groupe purgé

        var now = clock.GetUtcNow();
        var subscription = await billing.GetSubscriptionAsync(id, ct);
        if (subscription is null) billing.AddSubscription(Subscription.FromStripe(id, state, now));
        else subscription.Apply(state, now);

        if (paymentEvent.Type == PaymentFailed)
            jobs.Enqueue(PaymentFailedJob.JobType, id.ToString(), now);
        billing.AddProcessedEvent(ProcessedStripeEvent.Create(paymentEvent.Id, paymentEvent.Type, now));
        // Le même événement traité deux fois en même temps : la clé primaire refuse le second (409),
        // Stripe le renverra et il sera alors vu comme déjà traité.
        await unitOfWork.SaveChangesAsync(ct);
        return PaymentEventOutcome.Applied;
    }
}