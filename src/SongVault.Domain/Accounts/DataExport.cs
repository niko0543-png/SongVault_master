using SongVault.Domain.Common;

namespace SongVault.Domain.Accounts;

public enum DataExportStatus { Pending, Ready, Failed }

/// <summary>Une archive des données d'un compte (ADR 0011), téléchargeable <see cref="Lifetime"/> une fois prête.</summary>
public sealed class DataExport
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);
    public const int StorageKeyMaxLength = 100;

    private DataExport() { } // réservé à EF Core

    public Guid Id { get; private set; }
    public string UserId { get; private set; } = default!;
    public DataExportStatus Status { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public string? StorageKey { get; private set; }
    public long? SizeBytes { get; private set; }

    public static DataExport Request(string userId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new DomainException("Un export appartient à un compte.");

        return new DataExport
        {
            Id = Guid.CreateVersion7(now),
            UserId = userId,
            Status = DataExportStatus.Pending,
            RequestedAt = now,
        };
    }

    public void MarkReady(string storageKey, long sizeBytes, DateTimeOffset now)
    {
        if (Status != DataExportStatus.Pending)
            throw new DomainException("Cet export n'est plus en préparation.");

        (Status, StorageKey, SizeBytes, CompletedAt, ExpiresAt) =
            (DataExportStatus.Ready, storageKey, sizeBytes, now, now + Lifetime);
    }

    /// <summary>Dernier essai manqué. Sans effet si l'export n'est plus en préparation.</summary>
    public void MarkFailed(DateTimeOffset now)
    {
        if (Status != DataExportStatus.Pending) return;
        (Status, CompletedAt) = (DataExportStatus.Failed, now);
    }

    public bool IsDownloadableAt(DateTimeOffset now) => Status == DataExportStatus.Ready && now < ExpiresAt;
}