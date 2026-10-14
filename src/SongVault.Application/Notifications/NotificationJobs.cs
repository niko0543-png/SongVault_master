using System.Text.Json;

using Microsoft.Extensions.Options;

using SongVault.Application.Abstractions;
using SongVault.Application.Common;
using SongVault.Domain.Notifications;

namespace SongVault.Application.Notifications;

/// <summary>
/// Envoie un e-mail de notification (payload : SendNotificationPayload en JSON). Revérifie juste avant :
/// le destinataire a pu quitter le groupe ou changer d'avis depuis la préparation.
/// Un refus du fournisseur fait réessayer la tâche (trois essais, ADR 0011).
/// </summary>
public sealed class SendNotificationJob(
    INotificationRepository notifications, IEmailSender emailSender, IUnsubscribeTokens tokens,
    IOptions<AppOptions> app) : IBackgroundJobHandler
{
    public const string JobType = "notification.send";

    public string Type => JobType;

    public async Task RunAsync(BackgroundJobContext job, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<SendNotificationPayload>(job.Payload)
            ?? throw new InvalidOperationException("Contenu de tâche vide.");

        if (await notifications.GetEventsAsync([payload.EventId], ct) is not [var e]) return;   // groupe effacé
        if (await notifications.GetMemberAsync(e.BandId, payload.UserId, ct) is not { } to) return;

        var modes = await notifications.GetModesAsync([payload.UserId], ct);
        var mode = modes.TryGetValue((payload.UserId, payload.Kind), out var m) ? m : NotificationPreference.DefaultMode(payload.Kind);
        if (mode != NotificationMode.Immediate) return;

        await emailSender.SendAsync(new NotificationEmails(app.Value.PublicUrl, tokens).Immediate(to, payload.Kind, e), ct);
    }
}

/// <summary>
/// Résumé quotidien (ADR 0018) : un e-mail par compte qui a des éléments en attente, regroupés par groupe.
/// Les éléments d'un groupe quitté, ou d'un type qui n'est plus en résumé, sont effacés sans être envoyés.
/// Un refus du fournisseur fait réessayer la tâche pour les comptes restants. NotificationWorker programme le résumé suivant.
/// </summary>
public sealed class NotificationDigestJob(
    INotificationRepository notifications, IEmailSender emailSender, IUnsubscribeTokens tokens,
    IOptions<AppOptions> app) : IBackgroundJobHandler
{
    public const string JobType = "notification.digest";

    public string Type => JobType;

    public async Task RunAsync(BackgroundJobContext job, CancellationToken ct)
    {
        var emails = new NotificationEmails(app.Value.PublicUrl, tokens);
        foreach (var userId in await notifications.ListDigestUsersAsync(ct))
        {
            var entries = await notifications.ListDigestAsync(userId, ct);
            var modes = await notifications.GetModesAsync([userId], ct);
            var kept = entries
                .Where(x => x.IsMember
                            && (modes.TryGetValue((userId, x.Kind), out var mode) ? mode : NotificationPreference.DefaultMode(x.Kind))
                               == NotificationMode.Daily)
                .ToList();

            var email = await notifications.GetEmailAsync(userId, ct);
            if (kept.Count > 0 && email is not null)
                await emailSender.SendAsync(emails.Digest(userId, email, kept), ct);

            // Effacés compte par compte : si l'envoi suivant échoue, la tâche réessaie sans renvoyer celui-ci
            await notifications.DeleteDigestItemsAsync([.. entries.Select(x => x.ItemId)], ct);
        }
    }
}