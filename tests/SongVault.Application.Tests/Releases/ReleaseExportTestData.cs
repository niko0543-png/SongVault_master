using SongVault.Application.Tests.Fakes;
using SongVault.Domain.Files;
using SongVault.Domain.Releases;
using SongVault.Domain.Songs;

namespace SongVault.Application.Tests.Releases;

/// <summary>Une sortie de deux pistes : « Nuit / blanche » (master, deux pistes séparées, paroles) et « Aube » (master seul).</summary>
internal sealed class ReleaseExportTestData
{
    public static readonly DateTimeOffset Now = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);
    public static readonly Guid BandId = Guid.CreateVersion7();

    public FakeSongRepository Songs { get; } = new();
    public FakeReleaseRepository Releases { get; }
    public FakeFileScanRepository Scans { get; } = new();
    public FakeReleaseExportRepository Exports { get; }
    public FakeFileStorageService Storage { get; } = new();
    public FakeBackgroundJobs Jobs { get; } = new();
    public FakeUnitOfWork UnitOfWork { get; } = new();
    public FakeNotificationRepository Notifications { get; } = new();
    public FakeEmailSender Emails { get; } = new();

    public Release Release { get; }
    public SongVersion First { get; }
    public SongVersion Second { get; }

    public ReleaseExportTestData()
    {
        Releases = new FakeReleaseRepository(Songs);
        Exports = new FakeReleaseExportRepository(Releases, Songs, Scans);

        var first = Song.Create(BandId, "Nuit / blanche", null, null, Now);
        First = first.AddVersion("Mix final", SongVersionStatus.Final, null, "Couplet 1\nRefrain", Now);
        first.SetRights(null, [new CreditChoice("Alice Martin", CreditRole.Author, 100m)], Now);
        AddAudio(First, "master.wav", [1, 2, 3]);
        var voice = AddAudio(First, "voix.wav", [4, 5]);
        var drums = AddAudio(First, "batterie.wav", [6]);
        First.AddFile("partition.pdf", "k-pdf", "application/pdf", 3, SongFileType.Document, Now);
        First.SetStems([new StemChoice(voice.Id, "Voix"), new StemChoice(drums.Id, "Batterie")], Now);

        var second = Song.Create(BandId, "Aube", null, null, Now);
        Second = second.AddVersion("v1", SongVersionStatus.Final, null, null, Now);
        AddAudio(Second, "aube.mp3", [7, 8, 9, 10]);

        Songs.Songs.AddRange([first, second]);
        Release = Release.Create(BandId, "Nuits blanches", ReleaseType.EP, null, Now);
        Release.SetTracks([new ReleaseTrackChoice(first.Id, First.Id), new ReleaseTrackChoice(second.Id, Second.Id)], Now);
        Release.SetCover("k-cover", "image/png", Now);
        Storage.Files["k-cover"] = [0x89, 0x50];
        Releases.Releases.Add(Release);
    }

    public SongFile AddAudio(SongVersion version, string name, byte[] content, FileScanStatus status = FileScanStatus.Clean)
    {
        var key = $"k-{Guid.NewGuid():N}";
        var file = version.AddFile(name, key, "audio/wav", content.Length, SongFileType.Audio, Now);
        Storage.Files[key] = content;
        var scan = FileScan.Request(file.Id, Now);
        if (status == FileScanStatus.Clean) scan.MarkClean(Now);
        else if (status == FileScanStatus.Infected) scan.MarkInfected("Eicar", Now);
        Scans.Scans.Add(scan);
        return file;
    }
}