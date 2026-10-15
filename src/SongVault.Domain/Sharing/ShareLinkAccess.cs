namespace SongVault.Domain.Sharing;

/// <summary>
/// Une ligne du journal d'un lien. Ni adresse IP ni compte : une empreinte de l'IP, salée par lien,
/// suffit à distinguer les visiteurs sans les identifier (ADR 0019).
/// </summary>
public sealed class ShareLinkAccess
{
    public const int IpHashLength = 32;          // SHA-256
    public const int UserAgentMaxLength = 256;

    private ShareLinkAccess() { } // réservé à EF Core

    public long Id { get; private set; }
    public Guid ShareLinkId { get; private set; }
    public DateTimeOffset At { get; private set; }
    public ShareAccessKind Kind { get; private set; }
    /// <summary>Fichier écouté (Kind = Played), sans clé étrangère : la ligne reste si le fichier est supprimé.</summary>
    public Guid? FileId { get; private set; }
    public byte[]? IpHash { get; private set; }
    public string? UserAgent { get; private set; }

    public static ShareLinkAccess Create(Guid shareLinkId, ShareAccessKind kind, Guid? fileId,
        byte[]? ipHash, string? userAgent, DateTimeOffset now)
    {
        if (ipHash is not null && ipHash.Length != IpHashLength)
            throw new ArgumentException("Empreinte SHA-256 de 32 octets attendue.", nameof(ipHash));

        var agent = userAgent?.Trim();
        return new ShareLinkAccess
        {
            ShareLinkId = shareLinkId,
            At = now,
            Kind = kind,
            FileId = fileId,
            IpHash = ipHash,
            // Tronqué, jamais refusé : le journal ne doit pas faire échouer l'écoute
            UserAgent = string.IsNullOrEmpty(agent) ? null : agent[..Math.Min(agent.Length, UserAgentMaxLength)],
        };
    }
}