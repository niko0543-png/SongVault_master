using System.Security.Cryptography;

using Microsoft.AspNetCore.DataProtection;

using SongVault.Application.Abstractions;
using SongVault.Domain.Notifications;

namespace SongVault.Infrastructure.Notifications;

/// <summary>
/// Jetons de désinscription chiffrés et signés par Data Protection (clés en base, ADR 0010).
/// Le contenu est « identifiant|type » (« * » : tous les types). Pas d'expiration : un vieil e-mail doit
/// toujours permettre de se désinscrire, et le pire qu'un jeton volé permette est de couper des e-mails.
/// </summary>
internal sealed class UnsubscribeTokens(IDataProtectionProvider provider) : IUnsubscribeTokens
{
    private const string AllKinds = "*";
    private readonly IDataProtector _protector = provider.CreateProtector("SongVault.Notifications.Unsubscribe.v1");

    public string Create(string userId, NotificationKind? kind)
        => _protector.Protect($"{userId}|{kind?.ToString() ?? AllKinds}");

    public bool TryRead(string token, out string userId, out NotificationKind? kind)
    {
        userId = "";
        kind = null;
        string text;
        try
        {
            text = _protector.Unprotect(token);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return false;
        }

        var separator = text.LastIndexOf('|');
        if (separator <= 0) return false;
        var kindText = text[(separator + 1)..];
        if (kindText != AllKinds)
        {
            if (!Enum.TryParse<NotificationKind>(kindText, out var parsed) || !Enum.IsDefined(parsed)) return false;
            kind = parsed;
        }
        userId = text[..separator];
        return true;
    }
}