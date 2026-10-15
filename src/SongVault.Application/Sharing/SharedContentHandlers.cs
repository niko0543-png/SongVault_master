using System.Security.Cryptography;
using System.Text;

using SongVault.Application.Abstractions;
using SongVault.Application.Common.Exceptions;
using SongVault.Application.Files;
using SongVault.Application.Invitations;
using SongVault.Domain.Common;
using SongVault.Domain.Sharing;

namespace SongVault.Application.Sharing;

/// <summary>
/// Porte d'entrée des lectures publiques (ADR 0019). Lien inconnu, expiré ou révoqué : 404, sans dire lequel.
/// Lien protégé sans preuve du mot de passe : 403.
/// </summary>
public sealed class ShareLinkGate(IShareLinkRepository links, IShareUnlockTokens unlocks, TimeProvider clock)
{
    public async Task<ShareLink> FindActiveAsync(string token, CancellationToken ct)
    {
        var link = await links.GetByTokenHashAsync(InvitationToken.Hash(token), ct);
        if (link is null || !link.IsActiveAt(clock.GetUtcNow()))
            throw new NotFoundException("ShareLink", "lien");     // le jeton n'est jamais recopié dans le message
        return link;
    }

    public async Task<ShareLink> OpenAsync(string token, string? unlockToken, CancellationToken ct)
    {
        var link = await FindActiveAsync(token, ct);
        if (link.HasPassword && !IsUnlocked(link, unlockToken))
            throw new ForbiddenException("Ce lien est protégé par un mot de passe.");
        return link;
    }

    private bool IsUnlocked(ShareLink link, string? unlockToken)
        => !string.IsNullOrEmpty(unlockToken)
           && unlocks.TryRead(unlockToken, out var linkId, out var expiresAt)
           && linkId == link.Id
           && expiresAt > clock.GetUtcNow();
}

/// <summary>Écrit une ligne du journal d'un lien, avec l'empreinte salée de l'IP à la place de l'IP.</summary>
public sealed class ShareAccessLog(IShareLinkRepository links)
{
    public void Record(ShareLink link, ShareAccessKind kind, Guid? fileId, ShareVisitor visitor, DateTimeOffset now)
        => links.AddAccess(ShareLinkAccess.Create(link.Id, kind, fileId, HashIp(link.AccessSalt, visitor.IpAddress),
            visitor.UserAgent, now));

    /// <summary>SHA-256(sel du lien ‖ IP). null sans IP connue.</summary>
    public static byte[]? HashIp(byte[] salt, string? ipAddress)
        => string.IsNullOrWhiteSpace(ipAddress)
            ? null
            : SHA256.HashData([.. salt, .. Encoding.UTF8.GetBytes(ipAddress.Trim())]);
}

public sealed class OpenSharedContentHandler(
    ShareLinkGate gate, IShareLinkRepository links, ShareAccessLog log, IUnitOfWork unitOfWork, TimeProvider clock)
{
    /// <summary>Ce que le lien fait écouter. Chaque ouverture réussie entre au journal.</summary>
    public async Task<SharedContentDto> HandleAsync(string token, string? unlockToken, ShareVisitor visitor, CancellationToken ct)
    {
        var link = await gate.OpenAsync(token, unlockToken, ct);
        var content = await links.ReadContentAsync(link, ct) ?? throw new NotFoundException("ShareLink", "lien");
        log.Record(link, ShareAccessKind.Opened, null, visitor, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(ct);
        return content;
    }
}

public sealed class UnlockShareLinkHandler(
    ShareLinkGate gate, ISharePasswordHasher passwords, IShareUnlockTokens unlocks, ShareAccessLog log,
    IUnitOfWork unitOfWork, TimeProvider clock)
{
    /// <summary>Durée du déverrouillage d'un navigateur ; jamais au-delà de l'expiration du lien.</summary>
    public static readonly TimeSpan UnlockLifetime = TimeSpan.FromHours(12);

    /// <summary>Vérifie le mot de passe. Un échec entre au journal, puis 422.</summary>
    public async Task<ShareUnlockDto> HandleAsync(string token, string password, ShareVisitor visitor, CancellationToken ct)
    {
        var link = await gate.FindActiveAsync(token, ct);
        var now = clock.GetUtcNow();
        if (link.PasswordHash is { } hash && !passwords.Verify(hash, password ?? ""))
        {
            log.Record(link, ShareAccessKind.WrongPassword, null, visitor, now);
            await unitOfWork.SaveChangesAsync(ct);
            throw new DomainException("Mot de passe incorrect.");
        }

        var expiresAt = now + UnlockLifetime < link.ExpiresAt ? now + UnlockLifetime : link.ExpiresAt;
        return new ShareUnlockDto(unlocks.Create(link.Id, expiresAt), expiresAt);
    }
}

public sealed class DownloadSharedFileHandler(
    ShareLinkGate gate, IShareLinkRepository links, IFileStorageService storage, ShareAccessLog log,
    IUnitOfWork unitOfWork, TimeProvider clock)
{
    /// <summary>
    /// Fichier audio de la cible, à lire dans le navigateur (jamais en pièce jointe).
    /// countAsPlay : début de l'écoute, à journaliser ; faux pour les requêtes de reprise (Range au milieu du fichier).
    /// </summary>
    public async Task<FileDownload> HandleAsync(
        string token, string? unlockToken, Guid fileId, bool countAsPlay, ShareVisitor visitor, CancellationToken ct)
    {
        var link = await gate.OpenAsync(token, unlockToken, ct);
        var info = await links.GetSharedFileAsync(link, fileId, ct) ?? throw new NotFoundException("SongFile", fileId);

        if (countAsPlay)
        {
            log.Record(link, ShareAccessKind.Played, fileId, visitor, clock.GetUtcNow());
            await unitOfWork.SaveChangesAsync(ct);
        }

        // URL signée courte (stockage objet) : la révocation coupe les nouvelles écoutes, pas celle en cours
        var url = storage.CreateDownloadUrl(info.StorageKey, info.ContentType, null);
        if (url is not null) return new FileDownload(null, info.ContentType, info.OriginalFileName, url);
        return new FileDownload(await storage.OpenReadAsync(info.StorageKey, ct), info.ContentType, info.OriginalFileName);
    }
}

public sealed class DownloadSharedCoverHandler(ShareLinkGate gate, IShareLinkRepository links, IFileStorageService storage)
{
    /// <summary>Pochette de la sortie partagée. Pas de ligne au journal : elle s'affiche avec la page.</summary>
    public async Task<FileDownload> HandleAsync(string token, string? unlockToken, CancellationToken ct)
    {
        var link = await gate.OpenAsync(token, unlockToken, ct);
        var cover = await links.GetSharedCoverAsync(link, ct) ?? throw new NotFoundException("ReleaseCover", "lien");

        var url = storage.CreateDownloadUrl(cover.StorageKey, cover.ContentType, null);
        if (url is not null) return new FileDownload(null, cover.ContentType, cover.OriginalFileName, url);
        return new FileDownload(await storage.OpenReadAsync(cover.StorageKey, ct), cover.ContentType, cover.OriginalFileName);
    }
}