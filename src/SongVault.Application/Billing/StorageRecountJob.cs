using SongVault.Application.Abstractions;
using SongVault.Application.Notifications;

namespace SongVault.Application.Billing;

/// <summary>
/// Recalcule chaque nuit le stockage utilisé de tous les groupes (payload vide) : corrige un écart laissé par
/// un recalcul manqué (API arrêtée entre l'enregistrement et le recalcul, par exemple).
/// </summary>
public sealed class StorageRecountJob(IBillingRepository billing) : IBackgroundJobHandler
{
    public const string JobType = "billing.storage-recount";
    /// <summary>3 h, heure de Paris : loin des envois, et des résumés de notifications de 7 h.</summary>
    public const int Hour = 3;

    public string Type => JobType;

    public Task RunAsync(BackgroundJobContext job, CancellationToken ct) => billing.RefreshAllStorageUsedAsync(ct);

    /// <summary>Prochain passage, strictement après now.</summary>
    public static DateTimeOffset NextRun(DateTimeOffset now)
        => DigestSchedule.Next(now, Hour, TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris"));
}