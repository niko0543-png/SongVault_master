using SongVault.Application.Common.Exceptions;
using SongVault.Application.Tests.Fakes;
using SongVault.Application.Versions;
using SongVault.Domain.Songs;
using SongVault.Application.Activity;
using SongVault.Application.Billing;
using SongVault.Application.Files;
using SongVault.Domain.Common;
using SongVault.Domain.Files;

namespace SongVault.Application.Tests.Versions;

public sealed class CreateSongVersionHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 9, 0, 0, TimeSpan.Zero);
    private readonly FakeSongRepository _songs = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeFileStorageService _storage = new();
    private readonly FakeAudioPreviewRepository _previews = new();
    private readonly FakeFileScanRepository _scans = new();
    private readonly FakeBackgroundJobs _jobs = new();
    private readonly FakeBillingRepository _billing = new();

    private CreateSongVersionHandler Sut() => new(_songs, _uow, new FixedTimeProvider(Now),
       new ActivityLog(new FakeActivityRepository(), new FakeCurrentUser()), _storage,
       new AudioPreviewQueue(_previews), new FileScanQueue(_scans, _jobs), new PlanLimits(_billing));

    private CreateSongVersionCommand CommandForNewSong()
    {
        var song = Song.Create(Guid.NewGuid(), "Nocturne", null, null, Now);
        _songs.Songs.Add(song);
        return new CreateSongVersionCommand(song.Id, "v1", SongVersionStatus.Demo, null, null);
    }

    [Fact]
    public async Task Morceau_absent_leve_NotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Sut().HandleAsync(
            new CreateSongVersionCommand(Guid.NewGuid(), "v1", SongVersionStatus.Demo, null, null), CancellationToken.None));
    }

    [Fact]
    public async Task Un_conflit_puis_succes_retente_une_fois()
    {
        var command = CommandForNewSong();
        _uow.FailuresToSimulate = 1;

        await Sut().HandleAsync(command, CancellationToken.None);

        Assert.Equal(2, _uow.SaveCount);
        Assert.Equal(1, _uow.DiscardCount);
    }

    [Fact]
    public async Task Trois_conflits_propagent_l_exception()
    {
        var command = CommandForNewSong();
        _uow.FailuresToSimulate = 3;

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => Sut().HandleAsync(command, CancellationToken.None));

        Assert.Equal(3, _uow.SaveCount);
        Assert.Equal(2, _uow.DiscardCount);
    }

    // ---------- ADR 0047 : reprise de fichiers d'une version précédente ----------

    private (Song Song, SongVersion V1, SongFile Mix, SongFile Pdf) SongWithFiles()
    {
        var song = Song.Create(Guid.NewGuid(), "Nocturne", null, null, Now);
        var v1 = song.AddVersion("v1", SongVersionStatus.Demo, null, null, Now);
        var mix = v1.AddFile("mix.mp3", "a.mp3", "audio/mpeg", 3, SongFileType.Audio, Now);
        var pdf = v1.AddFile("paroles.pdf", "b.pdf", "application/pdf", 5, SongFileType.Document, Now);
        _storage.Files["a.mp3"] = [1, 2, 3];
        _storage.Files["b.pdf"] = [4, 5, 6, 7, 8];
        _songs.Songs.Add(song);
        _songs.Scans[mix.Id] = FileScanStatus.Clean;
        _songs.Scans[pdf.Id] = FileScanStatus.NotScanned;
        _billing.StorageUsed[song.BandId] = 8;
        return (song, v1, mix, pdf);
    }

    private static CreateSongVersionCommand Copy(Song song, SongVersion from, params SongFile[] files)
        => new(song.Id, "v2", SongVersionStatus.Demo, null, null, CopyFromVersionId: from.Id, CopyFileIds: [.. files.Select(f => f.Id)]);

    [Fact]
    public async Task Copie_les_fichiers_choisis_dans_le_stockage_et_les_fait_analyser()
    {
        var (song, v1, mix, _) = SongWithFiles();

        var created = await Sut().HandleAsync(Copy(song, v1, mix), CancellationToken.None);

        var copy = Assert.Single(created.Files);
        Assert.Equal("mix.mp3", copy.OriginalFileName);
        var v2 = song.FindVersion(created.Id)!;
        var key = Assert.Single(v2.Files).StorageKey;
        Assert.NotEqual("a.mp3", key);
        Assert.EndsWith(".mp3", key);
        Assert.Equal([1, 2, 3], _storage.Files[key]);
        Assert.Equal(copy.Id, Assert.Single(_scans.Scans).SongFileId);     // #17 : la copie repasse par l'antivirus
        Assert.Equal(copy.Id, Assert.Single(_previews.Previews).SongFileId);
        Assert.Equal(FileScanJob.JobType, Assert.Single(_jobs.Enqueued).Type);
        Assert.Equal(song.BandId, Assert.Single(_billing.Refreshed));
        Assert.Equal(1, _uow.SaveCount);
    }

    [Fact]
    public async Task Sans_version_d_origine_la_reprise_est_refusee()
    {
        var (song, _, mix, _) = SongWithFiles();

        await Assert.ThrowsAsync<DomainException>(() => Sut().HandleAsync(
            new CreateSongVersionCommand(song.Id, "v2", SongVersionStatus.Demo, null, null, CopyFileIds: [mix.Id]), CancellationToken.None));
        Assert.Equal(2, _storage.Files.Count);
    }

    [Fact]
    public async Task Un_fichier_bloque_par_l_antivirus_ne_se_reprend_pas()
    {
        var (song, v1, mix, pdf) = SongWithFiles();
        _songs.Scans[pdf.Id] = FileScanStatus.Infected;

        await Assert.ThrowsAsync<FileNotReadableException>(() => Sut().HandleAsync(Copy(song, v1, mix, pdf), CancellationToken.None));

        Assert.Equal(2, _storage.Files.Count);
        Assert.Equal(0, _uow.SaveCount);
    }

    [Fact]
    public async Task Une_reprise_qui_depasse_le_quota_ne_copie_rien()
    {
        var (song, v1, mix, pdf) = SongWithFiles();
        _billing.StorageUsed[song.BandId] = 995;                 // formule gratuite des fakes : 1000 octets

        await Assert.ThrowsAsync<PlanLimitException>(() => Sut().HandleAsync(Copy(song, v1, mix, pdf), CancellationToken.None));

        Assert.Equal(2, _storage.Files.Count);
    }

    [Fact]
    public async Task Un_echec_d_enregistrement_supprime_les_copies()
    {
        var (song, v1, mix, pdf) = SongWithFiles();
        _uow.FailuresToSimulate = 3;

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => Sut().HandleAsync(Copy(song, v1, mix, pdf), CancellationToken.None));

        Assert.Equal(["a.mp3", "b.pdf"], _storage.Files.Keys.Order());
        Assert.Empty(_billing.Refreshed);
    }
}
