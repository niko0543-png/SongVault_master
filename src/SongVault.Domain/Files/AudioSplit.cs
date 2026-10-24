using SongVault.Domain.Common;
using SongVault.Domain.Songs;

namespace SongVault.Domain.Files;

public enum AudioSplitStatus { Pending, Done, Failed }

/// <summary>
/// Un passage gardé d'un enregistrement découpé (ADR 0050), de StartMs à EndMs. SongId : morceau existant qui reçoit
/// une nouvelle version ; null : un nouveau morceau nommé Title. Une fois le découpage fait, CreatedSongId et
/// CreatedVersionId disent où le passage est arrivé.
/// </summary>
public sealed record AudioSplitPart(int StartMs, int EndMs, string Title, Guid? SongId,
    Guid? CreatedSongId = null, Guid? CreatedVersionId = null)
{
    public int DurationMs => EndMs - StartMs;
}

/// <summary>
/// Découpage d'un fichier audio en plusieurs morceaux (ADR 0050) : une répétition entière enregistrée d'un bloc
/// devient un fichier par morceau joué. Demandé par un membre, fait en tâche de fond par ffmpeg ; l'original
/// n'est jamais modifié.
/// </summary>
public sealed class AudioSplit
{
    public const int MaxParts = 30;
    /// <summary>Un passage plus court ne mérite pas d'être un morceau : sans doute un clic de trop.</summary>
    public const int MinPartMs = 1000;
    public const int ErrorMaxLength = 300;

    private AudioSplit() { } // réservé à EF Core

    public Guid Id { get; private set; }
    public Guid BandId { get; private set; }
    public Guid SourceFileId { get; private set; }
    /// <summary>Nom du fichier découpé au moment de la demande, pour nommer les passages.</summary>
    public string SourceFileName { get; private set; } = default!;
    public string RequestedById { get; private set; } = default!;
    public AudioSplitStatus Status { get; private set; }
    public IReadOnlyList<AudioSplitPart> Parts { get; private set; } = [];
    /// <summary>Raison lisible de l'échec, montrée aux membres du groupe.</summary>
    public string? Error { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <param name="durationMs">Durée du fichier, lue dans son aperçu : aucun passage ne la dépasse.</param>
    public static AudioSplit Request(Guid bandId, Guid sourceFileId, string sourceFileName, string requestedById,
        int durationMs, IReadOnlyList<AudioSplitPart> parts, DateTimeOffset now)
    {
        // 1. TOUT valider…
        if (bandId == Guid.Empty || sourceFileId == Guid.Empty)
            throw new DomainException("Un découpage porte sur un fichier du groupe.");
        if (string.IsNullOrWhiteSpace(requestedById))
            throw new DomainException("Un découpage est demandé par un membre.");
        if (durationMs <= 0)
            throw new DomainException("La durée du fichier n'est pas encore connue : attendez la fin de la préparation de l'aperçu.");
        if (parts.Count is 0 or > MaxParts)
            throw new DomainException($"Un découpage garde de 1 à {MaxParts} passages.");

        var clean = new List<AudioSplitPart>(parts.Count);
        foreach (var part in parts.OrderBy(p => p.StartMs))
        {
            if (part.StartMs < 0 || part.EndMs > durationMs)
                throw new DomainException("Un passage sort du fichier.");
            if (part.DurationMs < MinPartMs)
                throw new DomainException("Un passage dure au moins une seconde.");
            if (clean.Count > 0 && part.StartMs < clean[^1].EndMs)
                throw new DomainException("Deux passages se chevauchent.");
            var title = Text.Required(part.Title, Song.TitleMaxLength, "titre du passage");
            clean.Add(new AudioSplitPart(part.StartMs, part.EndMs, title, part.SongId == Guid.Empty ? null : part.SongId));
        }

        // 2. … PUIS affecter
        return new AudioSplit
        {
            Id = Guid.CreateVersion7(now),
            BandId = bandId,
            SourceFileId = sourceFileId,
            SourceFileName = Text.Required(sourceFileName, SongFile.OriginalFileNameMaxLength, "nom de fichier"),
            RequestedById = requestedById,
            Status = AudioSplitStatus.Pending,
            Parts = clean,
            RequestedAt = now,
        };
    }

    /// <summary>Chaque passage a reçu son morceau et sa version, dans l'ordre des passages.</summary>
    public void MarkDone(IReadOnlyList<(Guid SongId, Guid VersionId)> created, DateTimeOffset now)
    {
        if (Status != AudioSplitStatus.Pending) throw new DomainException("Ce découpage n'est plus en cours.");
        if (created.Count != Parts.Count) throw new DomainException("Chaque passage doit recevoir sa version.");

        Parts = [.. Parts.Select((p, i) => p with { CreatedSongId = created[i].SongId, CreatedVersionId = created[i].VersionId })];
        (Status, Error, CompletedAt) = (AudioSplitStatus.Done, null, now);
    }

    /// <summary>Échec définitif. Sans effet si le découpage n'est plus en cours.</summary>
    public void MarkFailed(string error, DateTimeOffset now)
    {
        if (Status != AudioSplitStatus.Pending) return;
        var reason = string.IsNullOrWhiteSpace(error) ? "Découpage impossible." : error.Trim();
        (Status, Error, CompletedAt) =
            (AudioSplitStatus.Failed, reason.Length > ErrorMaxLength ? reason[..ErrorMaxLength] : reason, now);
    }

    /// <summary>« Nocturne (repet-24-10).wav » : le nom du fichier d'un passage, avec l'extension choisie.</summary>
    public string PartFileName(AudioSplitPart part, string extension)
    {
        var source = Path.GetFileNameWithoutExtension(SourceFileName);
        var name = $"{part.Title} ({source})";
        var max = SongFile.OriginalFileNameMaxLength - extension.Length;
        return (name.Length > max ? name[..max] : name) + extension;
    }

    /// <summary>Titre de la version qui reçoit un passage : d'où il vient.</summary>
    public string PartVersionTitle()
    {
        var title = $"Extrait de {Path.GetFileNameWithoutExtension(SourceFileName)}";
        return title.Length > SongVersion.TitleMaxLength ? title[..SongVersion.TitleMaxLength] : title;
    }

    /// <summary>Notes de la version : le fichier et l'endroit d'où vient le passage.</summary>
    public string PartNotes(AudioSplitPart part)
        => $"Découpé dans « {SourceFileName} », de {Clock(part.StartMs)} à {Clock(part.EndMs)}.";

    private static string Clock(int ms)
    {
        var time = TimeSpan.FromMilliseconds(ms);
        return time.TotalHours >= 1 ? $"{(int)time.TotalHours}:{time:mm\\:ss}" : $"{(int)time.TotalMinutes}:{time:ss}";
    }
}
