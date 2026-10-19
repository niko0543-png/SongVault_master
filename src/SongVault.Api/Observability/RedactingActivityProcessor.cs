using System.Diagnostics;

using OpenTelemetry;

namespace SongVault.Api.Observability;

/// <summary>
/// Nettoie chaque trace avant son export (ADR 0025) : chemins et URL sans jeton ni e-mail,
/// et pas de navigateur (user_agent.original), inutile pour corriger une erreur.
/// </summary>
public sealed class RedactingActivityProcessor : BaseProcessor<Activity>
{
    private static readonly string[] DroppedTags = ["user_agent.original", "client.address", "http.client_ip"];

    public override void OnEnd(Activity activity)
    {
        foreach (var tag in DroppedTags)
            if (activity.GetTagItem(tag) is not null) activity.SetTag(tag, null);   // null retire l'attribut

        // Copie : on ne modifie pas une collection pendant qu'on la parcourt
        foreach (var (key, value) in activity.TagObjects.ToList())
        {
            var redacted = TelemetryRedactor.RedactValue(value);
            if (!ReferenceEquals(redacted, value)) activity.SetTag(key, redacted);
        }

        var name = TelemetryRedactor.Redact(activity.DisplayName);
        if (!ReferenceEquals(name, activity.DisplayName)) activity.DisplayName = name;
    }
}