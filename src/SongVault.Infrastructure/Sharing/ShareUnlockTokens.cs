using System.Globalization;
using System.Security.Cryptography;

using Microsoft.AspNetCore.DataProtection;

using SongVault.Application.Abstractions;

namespace SongVault.Infrastructure.Sharing;

/// <summary>
/// Jetons de déverrouillage chiffrés et signés par Data Protection (clés en base, ADR 0010).
/// Contenu : « identifiant du lien|fin en secondes Unix ». La fin est vérifiée avec l'horloge de l'application.
/// </summary>
internal sealed class ShareUnlockTokens(IDataProtectionProvider provider) : IShareUnlockTokens
{
    private readonly IDataProtector _protector = provider.CreateProtector("SongVault.Sharing.Unlock.v1");

    public string Create(Guid linkId, DateTimeOffset expiresAt)
        => _protector.Protect(string.Create(CultureInfo.InvariantCulture, $"{linkId:N}|{expiresAt.ToUnixTimeSeconds()}"));

    public bool TryRead(string token, out Guid linkId, out DateTimeOffset expiresAt)
    {
        linkId = Guid.Empty;
        expiresAt = default;
        string text;
        try
        {
            text = _protector.Unprotect(token);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return false;
        }

        var parts = text.Split('|');
        if (parts.Length != 2
            || !Guid.TryParseExact(parts[0], "N", out var id)
            || !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
            return false;

        (linkId, expiresAt) = (id, DateTimeOffset.FromUnixTimeSeconds(seconds));
        return true;
    }
}