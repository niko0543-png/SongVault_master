using SongVault.Domain.Common;

namespace SongVault.Domain.Files;

/// <summary>NotScanned : antivirus désactivé (Antivirus:Provider None), fichier lisible sans verdict.</summary>
public enum FileScanStatus { Pending, Clean, Infected, Failed, NotScanned }

/// <summary>
/// Analyse antivirus d'un fichier (ADR 0024). Tant qu'elle n'est ni Clean ni NotScanned, le fichier n'est ni lu,
/// ni téléchargé, ni partagé, ni converti. Un fichier Infected reste en quarantaine jusqu'à sa suppression.
/// </summary>
public sealed class FileScan
{
    public const int ThreatMaxLength = 200;
    public const int ErrorMaxLength = 300;

    private FileScan() { } // réservé à EF Core

    public Guid SongFileId { get; private set; }
    public FileScanStatus Status { get; private set; }
    /// <summary>Nom de la menace donné par l'antivirus (Infected seulement), par exemple « Win.Test.EICAR_HDB-1 ».</summary>
    public string? Threat { get; private set; }
    /// <summary>Raison lisible du dernier échec (Failed seulement).</summary>
    public string? Error { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public bool IsReadable => Status is FileScanStatus.Clean or FileScanStatus.NotScanned;

    public static FileScan Request(Guid songFileId, DateTimeOffset now)
    {
        if (songFileId == Guid.Empty) throw new DomainException("Une analyse porte sur un fichier.");
        return new FileScan { SongFileId = songFileId, Status = FileScanStatus.Pending, RequestedAt = now };
    }

    public void MarkClean(DateTimeOffset now)
    {
        EnsurePending();
        (Status, Threat, Error, CompletedAt) = (FileScanStatus.Clean, null, null, now);
    }

    public void MarkInfected(string threat, DateTimeOffset now)
    {
        EnsurePending();
        var name = string.IsNullOrWhiteSpace(threat) ? "Menace inconnue" : threat.Trim();
        (Status, Threat, Error, CompletedAt) =
            (FileScanStatus.Infected, name.Length > ThreatMaxLength ? name[..ThreatMaxLength] : name, null, now);
    }

    /// <summary>Antivirus désactivé : le fichier devient lisible sans verdict, en attendant l'activation.</summary>
    public void MarkNotScanned(DateTimeOffset now)
    {
        EnsurePending();
        (Status, Threat, Error, CompletedAt) = (FileScanStatus.NotScanned, null, null, now);
    }

    /// <summary>
    /// Antivirus activé : un fichier jamais analysé repasse en attente. Comme au premier envoi,
    /// il redevient illisible jusqu'au verdict.
    /// </summary>
    public void RequestAfterActivation(DateTimeOffset now)
    {
        if (Status != FileScanStatus.NotScanned) throw new DomainException("Seul un fichier non analysé attend l'antivirus.");
        (Status, CompletedAt, RequestedAt) = (FileScanStatus.Pending, null, now);
    }

    /// <summary>Dernier essai manqué (antivirus injoignable, par exemple). Sans effet si l'analyse n'est plus en cours.</summary>
    public void MarkFailed(string error, DateTimeOffset now)
    {
        if (Status != FileScanStatus.Pending) return;
        var reason = string.IsNullOrWhiteSpace(error) ? "Analyse impossible." : error.Trim();
        (Status, Error, CompletedAt) =
            (FileScanStatus.Failed, reason.Length > ErrorMaxLength ? reason[..ErrorMaxLength] : reason, now);
    }

    /// <summary>Relance après un échec, à la demande d'un membre. Un fichier infecté n'est jamais relancé.</summary>
    public void Retry(DateTimeOffset now)
    {
        if (Status != FileScanStatus.Failed) throw new DomainException("Seule une analyse en échec peut être relancée.");
        (Status, Error, CompletedAt, RequestedAt) = (FileScanStatus.Pending, null, null, now);
    }

    private void EnsurePending()
    {
        if (Status != FileScanStatus.Pending) throw new DomainException("Ce fichier n'est plus en cours d'analyse.");
    }
}