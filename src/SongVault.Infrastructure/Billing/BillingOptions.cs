namespace SongVault.Infrastructure.Billing;

public sealed class BillingOptions
{
    public const string SectionName = "Billing";
    public const string NoneProvider = "None";
    public const string StripeProvider = "Stripe";

    /// <summary>None (défaut : formules et quotas, sans paiement en ligne) ou Stripe.</summary>
    public string Provider { get; set; } = NoneProvider;
    public StripeOptions Stripe { get; set; } = new();
}

public sealed class StripeOptions
{
    /// <summary>Clé secrète (sk_test_… ou sk_live_…). Secret : user-secrets ou secret du Container App, jamais ici.</summary>
    public string SecretKey { get; set; } = "";
    /// <summary>Secret de signature du webhook (whsec_…), donné par Stripe pour l'adresse du webhook ou par stripe listen.</summary>
    public string WebhookSecret { get; set; } = "";
    /// <summary>Prix Stripe de chaque formule payante : identifiant de formule → identifiant de prix (price_…).</summary>
    public Dictionary<string, string> Prices { get; set; } = [];
    /// <summary>TVA calculée par Stripe Tax (payant). Faux tant que le vendeur est en franchise en base de TVA.</summary>
    public bool AutomaticTax { get; set; }
    /// <summary>Case « J'accepte les conditions » sur la page de paiement : l'adresse des CGV doit être réglée dans Stripe.</summary>
    public bool RequireTermsConsent { get; set; } = true;
    /// <summary>Version de l'API Stripe, fixée : une nouvelle version de Stripe ne change pas les réponses sans prévenir.</summary>
    public string ApiVersion { get; set; } = "2025-03-31.basil";
    /// <summary>Âge maximal d'une signature de webhook : au-delà, un événement rejoué est refusé.</summary>
    public TimeSpan SignatureTolerance { get; set; } = TimeSpan.FromMinutes(5);
}