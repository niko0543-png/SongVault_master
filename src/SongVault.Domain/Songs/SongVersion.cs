using SongVault.Domain.Common;

namespace SongVault.Domain.Songs;

public sealed class SongVersion
{
    public const int TitleMaxLength = 200;
    public const int NotesMaxLength = 4000;
    public const int LyricsMaxLength = 20000;

    private SongVersion() { }

    public Guid Id { get; private set; }
    public Guid SongId { get; private set; }
    public int Number { get; private set; }
    public string Title { get; private set; } = default!;
    public SongVersionStatus Status { get; private set; }
    public string? Notes { get; private set; }
    public string? Lyrics { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // internal : seul Song (même assembly) peut créer une version
    internal static SongVersion Create(Guid songId, int number, string title, SongVersionStatus status,
        string? notes, string? lyrics, DateTimeOffset now)
    {
        var version = new SongVersion { Id = Guid.CreateVersion7(now), SongId = songId, Number = number, CreatedAt = now };
        version.Apply(title, status, notes, lyrics, now);
        return version;
    }

    public void Update(string title, SongVersionStatus status, string? notes, string? lyrics, DateTimeOffset now)
        => Apply(title, status, notes, lyrics, now);

    private void Apply(string title, SongVersionStatus status, string? notes, string? lyrics, DateTimeOffset now)
    {
        var cleanTitle = Text.Required(title, TitleMaxLength, "titre");
        var cleanNotes = Text.Optional(notes, NotesMaxLength, "notes");
        var cleanLyrics = Text.Optional(lyrics, LyricsMaxLength, "paroles");
        if (!Enum.IsDefined(status))
            throw new DomainException($"Statut de version inconnu : {status}.");

        Title = cleanTitle;
        Status = status;
        Notes = cleanNotes;
        Lyrics = cleanLyrics;
        UpdatedAt = now;
    }
}