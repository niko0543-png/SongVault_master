using SongVault.Application.Abstractions;
using SongVault.Domain.Notifications;

namespace SongVault.Application.Notifications;

/// <summary>Le mode choisi pour un type ; IsDefault : le compte n'a encore rien choisi.</summary>
public sealed record NotificationPreferenceDto(NotificationKind Kind, NotificationMode Mode, bool IsDefault);

public sealed class GetNotificationPreferencesHandler(INotificationRepository notifications, ICurrentUser user)
{
    /// <summary>Un mode par type, dans l'ordre de l'enum, mode par défaut compris.</summary>
    public async Task<IReadOnlyList<NotificationPreferenceDto>> HandleAsync(CancellationToken ct)
        => Merge(await notifications.ListPreferencesAsync(user.UserId, ct));

    internal static IReadOnlyList<NotificationPreferenceDto> Merge(IReadOnlyList<NotificationPreference> saved)
        => [.. Enum.GetValues<NotificationKind>().Select(kind =>
            saved.FirstOrDefault(p => p.Kind == kind) is { } p
                ? new NotificationPreferenceDto(kind, p.Mode, false)
                : new NotificationPreferenceDto(kind, NotificationPreference.DefaultMode(kind), true))];
}

public sealed class UpdateNotificationPreferencesHandler(
    INotificationRepository notifications, IUnitOfWork unitOfWork, ICurrentUser user)
{
    /// <summary>Enregistre les modes donnés ; un type absent garde son réglage.</summary>
    public async Task<IReadOnlyList<NotificationPreferenceDto>> HandleAsync(
        IReadOnlyDictionary<NotificationKind, NotificationMode> modes, CancellationToken ct)
    {
        var saved = await NotificationPreferenceWriter.SetAsync(notifications, user.UserId, modes, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return GetNotificationPreferencesHandler.Merge(saved);
    }
}

/// <summary>Ce que fait un lien de désinscription : le type coupé, ou null pour tous.</summary>
public sealed record UnsubscribeResult(NotificationKind? Kind);

/// <summary>Lien de désinscription d'un e-mail (ADR 0018), utilisable sans être connecté.</summary>
public sealed class UnsubscribeHandler(
    INotificationRepository notifications, IUnsubscribeTokens tokens, IUnitOfWork unitOfWork)
{
    /// <summary>Passe le type (ou tous) à Off. null : jeton invalide. Recommencer ne change rien.</summary>
    public async Task<UnsubscribeResult?> HandleAsync(string token, CancellationToken ct)
    {
        if (!tokens.TryRead(token, out var userId, out var kind)) return null;
        if (await notifications.GetEmailAsync(userId, ct) is null) return new UnsubscribeResult(kind);   // compte purgé

        var kinds = kind is { } k ? [k] : Enum.GetValues<NotificationKind>();
        await NotificationPreferenceWriter.SetAsync(
            notifications, userId, kinds.ToDictionary(x => x, _ => NotificationMode.Off), ct);
        await unitOfWork.SaveChangesAsync(ct);
        return new UnsubscribeResult(kind);
    }
}

internal static class NotificationPreferenceWriter
{
    /// <summary>Crée ou modifie les préférences, sans enregistrer ; renvoie toutes celles du compte.</summary>
    public static async Task<IReadOnlyList<NotificationPreference>> SetAsync(INotificationRepository notifications,
        string userId, IReadOnlyDictionary<NotificationKind, NotificationMode> modes, CancellationToken ct)
    {
        var saved = (await notifications.ListPreferencesAsync(userId, ct)).ToList();
        foreach (var (kind, mode) in modes)
        {
            if (saved.FirstOrDefault(p => p.Kind == kind) is { } existing)
            {
                existing.ChangeTo(mode);
                continue;
            }
            var created = NotificationPreference.Create(userId, kind, mode);
            notifications.AddPreference(created);
            saved.Add(created);
        }
        return saved;
    }
}