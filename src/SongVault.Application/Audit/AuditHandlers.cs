using SongVault.Application.Abstractions;
using SongVault.Application.Activity;

namespace SongVault.Application.Audit;

public sealed class ListAuditHandler(IAuditRepository entries)
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 100;

    /// <summary>
    /// Journal du groupe actif (Owner, vérifié par [MinimumBandRole]), du plus récent au plus ancien.
    /// Même curseur que le fil d'activité : la date et le numéro de la dernière ligne affichée.
    /// </summary>
    public async Task<AuditPage> HandleAsync(ActivityCursor? after, int pageSize, CancellationToken ct)
    {
        var size = Math.Clamp(pageSize, 1, MaxPageSize);
        var items = await entries.ListAsync(after, size + 1, ct);       // un de plus : reste-t-il une page ?
        if (items.Count <= size) return new AuditPage(items, null);

        var page = items.Take(size).ToList();
        var last = page[^1];
        return new AuditPage(page, new ActivityCursor(last.OccurredAt, last.Id).ToString());
    }
}