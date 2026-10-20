using SongVault.Domain.Common;

namespace SongVault.Domain.Releases;

public enum ReleaseExportStatus { Pending, Ready, Failed }

/// <summary>
/// Archive ZIP d'une sortie pour le distributeur (#24, ADR 0031), téléchargeable <see cref="Lifetime"/> une fois prête.
/// La clé de stockage se déduit de l'identifiant : la tâche d'expiration efface l'archive même si la ligne
/// est partie avec sa sortie.
/// </summary>
public sealed class ReleaseExport
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    private ReleaseExport() { } // réservé à EF Core

    public Guid Id { get; private set; }
    public Guid BandId { get; private set; }
    public Guid ReleaseId { get; private set; }
    public string RequestedById { get; private set; } = default!;
    public ReleaseExportStatus Status { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public long? SizeBytes { get; private set; }

    public string StorageKey => StorageKeyFor(Id);

    public static string StorageKeyFor(Guid exportId) => $"release-export-{exportId:N}.zip";

    public static ReleaseExport Request(Guid bandId, Guid releaseId, string requestedById, DateTimeOffset now)
    {
        if (bandId == Guid.Empty || releaseId == Guid.Empty)
            throw new DomainException("Une archive appartient à une sortie du groupe.");
        if (string.IsNullOrWhiteSpace(requestedById))
            throw new DomainException("Une archive est demandée par un membre.");

        return new ReleaseExport
        {
            Id = Guid.CreateVersion7(now),
            BandId = bandId,
            ReleaseId = releaseId,
            RequestedById = requestedById,
            Status = ReleaseExportStatus.Pending,
            RequestedAt = now,
        };
    }

    public void MarkReady(long sizeBytes, DateTimeOffset now)
    {
        if (Status != ReleaseExportStatus.Pending)
            throw new DomainException("Cette archive n'est plus en préparation.");
        if (sizeBytes <= 0)
            throw new DomainException("Une archive prête a une taille.");

        (Status, SizeBytes, CompletedAt, ExpiresAt) = (ReleaseExportStatus.Ready, sizeBytes, now, now + Lifetime);
    }

    /// <summary>Dernier essai manqué. Sans effet si l'archive n'est plus en préparation.</summary>
    public void MarkFailed(DateTimeOffset now)
    {
        if (Status != ReleaseExportStatus.Pending) return;
        (Status, CompletedAt) = (ReleaseExportStatus.Failed, now);
    }

    public bool IsDownloadableAt(DateTimeOffset now) => Status == ReleaseExportStatus.Ready && now < ExpiresAt;

    /// <summary>Prête, mais passée : le lien répond 410 plutôt que 404.</summary>
    public bool IsExpiredAt(DateTimeOffset now) => Status == ReleaseExportStatus.Ready && now >= ExpiresAt;
}