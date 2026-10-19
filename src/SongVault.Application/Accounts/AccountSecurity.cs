using System.Net;
using System.Net.Sockets;

using SongVault.Application.Abstractions;
using SongVault.Domain.Accounts;

namespace SongVault.Application.Accounts;

/// <summary>Une ligne de l'historique, telle que la montre « Mon compte ».</summary>
public sealed record AccountEventDto(long Id, AccountEventType Type, DateTimeOffset OccurredAt, string? Network, string? Device);

/// <summary>D'où vient la requête : IP du client (après nginx) et en-tête User-Agent, bruts.</summary>
public sealed record ClientInfo(string? IpAddress, string? UserAgent);

/// <summary>
/// Écrit l'historique de connexion (#19, ADR 0026) et l'enregistre aussitôt : il est appelé pendant la connexion
/// elle-même, en dehors de tout handler. Rien de brut n'est gardé : réseau masqué, navigateur résumé.
/// </summary>
public sealed class AccountSecurityLog(IAccountEventRepository events, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public async Task RecordAsync(string userId, AccountEventType type, ClientInfo client, CancellationToken ct)
    {
        events.Add(AccountEvent.Record(userId, type, ClientLabels.Network(client.IpAddress),
            ClientLabels.Device(client.UserAgent), clock.GetUtcNow()));
        await unitOfWork.SaveChangesAsync(ct);
    }
}

public sealed class ListAccountEventsHandler(IAccountEventRepository events, ICurrentUser user)
{
    /// <summary>Lignes montrées au plus : de quoi repérer une connexion inconnue sans charger 90 jours.</summary>
    public const int MaxEntries = 50;

    public Task<IReadOnlyList<AccountEventDto>> HandleAsync(CancellationToken ct)
        => events.ListAsync(user.UserId, MaxEntries, ct);
}

/// <summary>Ce qu'on garde d'une IP et d'un User-Agent : assez pour se reconnaître, pas pour identifier.</summary>
public static class ClientLabels
{
    /// <summary>IPv4 → son /24 (« 203.0.113.0/24 ») ; IPv6 → son /48. null si absente ou illisible.</summary>
    public static string? Network(string? ipAddress)
    {
        if (!IPAddress.TryParse(ipAddress?.Trim(), out var ip)) return null;
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();

        var bytes = ip.GetAddressBytes();
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            bytes[3] = 0;
            return $"{new IPAddress(bytes)}/24";
        }
        Array.Clear(bytes, 6, bytes.Length - 6);
        return $"{new IPAddress(bytes)}/48";
    }

    /// <summary>« Firefox sur Windows », « Safari sur iOS »… ; null si l'en-tête ne dit rien de connu.</summary>
    public static string? Device(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return null;
        var ua = userAgent;

        // L'ordre compte : Edge et Opera se disent aussi Chrome, Chrome se dit aussi Safari
        string? browser =
            Has(ua, "Edg/") || Has(ua, "EdgA/") || Has(ua, "EdgiOS/") ? "Edge"
            : Has(ua, "OPR/") || Has(ua, "Opera") ? "Opera"
            : Has(ua, "Firefox/") || Has(ua, "FxiOS/") ? "Firefox"
            : Has(ua, "Chrome/") || Has(ua, "CriOS/") ? "Chrome"
            : Has(ua, "Safari/") ? "Safari"
            : null;
        string? system =
            Has(ua, "iPhone") || Has(ua, "iPad") ? "iOS"
            : Has(ua, "Android") ? "Android"
            : Has(ua, "Windows") ? "Windows"
            : Has(ua, "Mac OS X") || Has(ua, "Macintosh") ? "macOS"
            : Has(ua, "CrOS") ? "ChromeOS"
            : Has(ua, "Linux") ? "Linux"
            : null;

        return (browser, system) switch
        {
            (null, null) => null,
            (_, null) => browser,
            (null, _) => $"Navigateur sur {system}",
            _ => $"{browser} sur {system}",
        };
    }

    private static bool Has(string ua, string token) => ua.Contains(token, StringComparison.OrdinalIgnoreCase);
}