using SongVault.Application.Abstractions;
using SongVault.Application.Billing;
using SongVault.Application.Common.Exceptions;
using SongVault.Application.Files;
using SongVault.Application.Files.Splits;
using SongVault.Application.Tests.Fakes;
using SongVault.Domain.Activity;
using SongVault.Domain.Billing;
using SongVault.Domain.Common;
using SongVault.Domain.Files;
using SongVault.Domain.Songs;

namespace SongVault.Application.Tests.Files.Splits;

public sealed class AudioSplitTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 20, 0, 0, TimeSpan.Zero);
    private static readonly Guid BandId = Guid.NewGuid();
    private readonly FakeSongRepository _songs = new();
    private readonly FakeAudioSplitRepository _splits;
    private readonly FakeAudioPreviewRepository _previews = new();
    private readonly FakeFileScanRepository _scans = new();
    private readonly FakeBackgroundJobs _jobs = new();
    private readonly FakeFileStorageService _storage = new();
    private readonly FakeAudioProcessor _processor = new();
    private readonly FakeActivityRepository _activity = new();
    private readonly FakeBillingRepository _billing = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly Guid _fileId = Guid.NewGuid();

    public AudioSplitTests()
    {
        _splits = new FakeAudioSplitRepository(_songs);
        // Une heure de WAV ne tient pas dans les 1000 octets de la formule gratuite des fakes : 10 Go ici
        _billing.Plans[0] = Plan.Create(Plan.FreeId, "Gratuit", 10_000_000_000, maxBandsPerOwner: 1, monthlyPriceCents: 0, sortOrder: 1);
        _billing.StorageUsed[BandId] = 0;
    }

    private RequestAudioSplitHandler Request() => new(_splits, new FakeBandContext(BandId), new FakeCurrentUser("member-1"),
        _jobs, _uow, new PlanLimits(_billing), new FixedTimeProvider(Now));

    private AudioSplitJob Job() => new(_splits, _previews, _storage, _processor, new AudioPreviewQueue(_previews),
        new FileScanQueue(_scans, _jobs), _activity, new PlanLimits(_billing), _uow, new FixedTimeProvider(Now));

    /// <summary>Une heure de répétition en WAV, de 600 Mo, lisible et à l'aperçu prêt.</summary>
    private void Recorded(string fileName = "repet-24-10.wav", AudioPreviewStatus? preview = AudioPreviewStatus.Ready,
        FileScanStatus scan = FileScanStatus.Clean)
    {
        _splits.Sources[_fileId] = new AudioSplitSource(_fileId, fileName, 600_000_000, SongFileType.Audio, scan, preview,
            preview == AudioPreviewStatus.Ready ? 3_600_000 : null);
        var key = $"source{Path.GetExtension(fileName)}";
        _previews.Sources[_fileId] = key;
        _storage.Files[key] = [1, 2, 3];
    }

    private Song ExistingSong(string title = "Nocturne")
    {
        var song = Song.Create(BandId, title, null, null, Now.AddDays(-10));
        song.AddVersion("Maquette", SongVersionStatus.Demo, null, null, Now.AddDays(-10));
        _songs.Add(song);
        return song;
    }

    private static RequestAudioSplitCommand Command(params AudioSplitPartCommand[] parts)
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, parts);

    private Task<AudioSplitDto> RequestAsync(params AudioSplitPartCommand[] parts)
        => Request().HandleAsync(Command(parts) with { FileId = _fileId }, CancellationToken.None);

    private Task RunAsync(int attempt = 1)
        => Job().RunAsync(new BackgroundJobContext(Guid.NewGuid(), _splits.Splits.Single().Id.ToString(), attempt, attempt == 3),
            CancellationToken.None);

    // ---------- Demande ----------

    [Fact]
    public async Task La_demande_programme_la_tache_et_reste_en_cours()
    {
        Recorded();

        var split = await RequestAsync(new AudioSplitPartCommand(0, 240_000, "Intro", null), new(300_000, 600_000, "Nocturne", ExistingSong().Id));

        Assert.Equal(AudioSplitStatus.Pending, split.Status);
        Assert.Equal(2, split.Parts.Count);
        Assert.Contains(_jobs.Enqueued, j => j.Type == AudioSplitJob.JobType && j.Payload == split.Id.ToString());
        Assert.Equal("member-1", _splits.Splits.Single().RequestedById);
        Assert.Equal(1, _uow.SaveCount);
    }

    [Fact]
    public async Task Refuse_tant_que_l_apercu_n_est_pas_pret()
    {
        Recorded(preview: AudioPreviewStatus.Pending);

        await Assert.ThrowsAsync<ConflictException>(() => RequestAsync(new AudioSplitPartCommand(0, 60_000, "A", null)));
    }

    [Fact]
    public async Task Refuse_un_fichier_pas_declare_sain()
    {
        Recorded(scan: FileScanStatus.Pending);

        await Assert.ThrowsAsync<FileNotReadableException>(() => RequestAsync(new AudioSplitPartCommand(0, 60_000, "A", null)));
    }

    [Fact]
    public async Task Refuse_un_second_decoupage_pendant_le_premier()
    {
        Recorded();
        await RequestAsync(new AudioSplitPartCommand(0, 60_000, "A", null));

        await Assert.ThrowsAsync<ConflictException>(() => RequestAsync(new AudioSplitPartCommand(0, 60_000, "A", null)));
    }

    [Fact]
    public async Task Refuse_un_morceau_d_un_autre_groupe_ou_a_la_corbeille()
    {
        Recorded();
        var other = Song.Create(Guid.NewGuid(), "Ailleurs", null, null, Now);
        _songs.Add(other);

        await Assert.ThrowsAsync<DomainException>(() => RequestAsync(new AudioSplitPartCommand(0, 60_000, "A", other.Id)));
    }

    [Fact]
    public async Task Refuse_des_passages_qui_ne_tiennent_pas_dans_le_stockage()
    {
        Recorded();
        _billing.StorageUsed[BandId] = 10_000_000_000 - 1000;         // presque plein

        await Assert.ThrowsAsync<PlanLimitException>(() => RequestAsync(new AudioSplitPartCommand(0, 60_000, "A", null)));
        Assert.Empty(_splits.Splits);
    }

    [Fact]
    public async Task L_estimation_suit_la_part_gardee_de_l_original_ou_le_debit_du_mp3()
    {
        var wav = new AudioSplitSource(_fileId, "r.wav", 600_000_000, SongFileType.Audio, null, null, 3_600_000);
        var webm = wav with { FileName = "r.webm", SizeBytes = 50_000_000 };

        Assert.Equal(100_000_000, AudioSplitFormats.EstimateBytes(wav, 600_000));          // un sixième
        Assert.Equal(14_400_000, AudioSplitFormats.EstimateBytes(webm, 600_000));          // 10 min à 192 kbit/s
    }

    // ---------- Tâche ----------

    [Fact]
    public async Task Chaque_passage_devient_une_version_d_un_nouveau_morceau_ou_d_un_morceau_existant()
    {
        Recorded();
        var nocturne = ExistingSong();
        await RequestAsync(new AudioSplitPartCommand(0, 240_000, "Intro", null), new(300_000, 600_000, "Nocturne", nocturne.Id));

        await RunAsync();

        var split = _splits.Splits.Single();
        Assert.Equal(AudioSplitStatus.Done, split.Status);
        var intro = _songs.Songs.Single(s => s.Title == "Intro");
        Assert.Equal(BandId, intro.BandId);
        var introVersion = Assert.Single(intro.Versions);
        Assert.Equal(SongVersionStatus.Rehearsal, introVersion.Status);
        Assert.Equal("Extrait de repet-24-10", introVersion.Title);
        var introFile = Assert.Single(introVersion.Files);
        Assert.Equal("Intro (repet-24-10).wav", introFile.OriginalFileName);
        Assert.Equal("0-240000", System.Text.Encoding.UTF8.GetString(_storage.Files[introFile.StorageKey]));

        var newVersion = nocturne.Versions.Single(v => v.Number == 2);
        Assert.Equal(newVersion.Id, split.Parts[1].CreatedVersionId);
        Assert.Equal(nocturne.Id, split.Parts[1].CreatedSongId);
        Assert.Equal("Découpé dans « repet-24-10.wav », de 5:00 à 10:00.", newVersion.Notes);

        Assert.Equal(AudioCutFormat.Wav, _processor.Splits.Single().Format);
        Assert.Equal([1, 2, 3], _storage.Files["source.wav"]);                       // l'original reste tel quel
        Assert.Equal(2, _previews.Previews.Count);                                  // aperçu (#15)
        Assert.Equal(2, _scans.Scans.Count);                                        // antivirus (#17)
        Assert.Equal([ActivityType.SongCreated, ActivityType.VersionAdded, ActivityType.VersionAdded],
            _activity.Events.Select(e => e.Type));
        Assert.All(_activity.Events, e => Assert.Equal("member-1", e.ActorId));
        Assert.Contains(BandId, _billing.Refreshed);
    }

    [Fact]
    public async Task Deux_passages_vers_le_meme_morceau_donnent_deux_versions()
    {
        Recorded();
        var nocturne = ExistingSong();
        await RequestAsync(new AudioSplitPartCommand(0, 240_000, "Prise 1", nocturne.Id), new(300_000, 600_000, "Prise 2", nocturne.Id));

        await RunAsync();

        Assert.Equal([1, 2, 3], nocturne.Versions.Select(v => v.Number));
    }

    [Theory]
    [InlineData("repet.mp3", AudioCutFormat.Mp3, ".mp3")]
    [InlineData("repet.flac", AudioCutFormat.Flac, ".flac")]
    [InlineData("repet.webm", AudioCutFormat.Mp3Encoded, ".mp3")]
    public async Task Le_format_des_passages_suit_celui_de_l_original(string fileName, AudioCutFormat format, string extension)
    {
        Recorded(fileName);
        await RequestAsync(new AudioSplitPartCommand(0, 60_000, "A", null));

        await RunAsync();

        Assert.Equal(format, _processor.Splits.Single().Format);
        Assert.EndsWith(extension, _songs.Songs.Single().Versions.Single().Files.Single().OriginalFileName);
    }

    [Fact]
    public async Task Un_morceau_mis_a_la_corbeille_entre_temps_fait_echouer_sans_rien_laisser()
    {
        Recorded();
        var nocturne = ExistingSong();
        await RequestAsync(new AudioSplitPartCommand(0, 240_000, "Intro", null), new(300_000, 600_000, "Nocturne", nocturne.Id));
        nocturne.MoveToTrash("member-2", Now);

        await RunAsync();                                                  // pas d'exception : un nouvel essai n'y changerait rien

        var split = _splits.Splits.Single();
        Assert.Equal(AudioSplitStatus.Failed, split.Status);
        Assert.Contains("Nocturne", split.Error);
        Assert.Single(_storage.Files);                                     // seul l'original reste
        Assert.Equal(1, _uow.DiscardCount);
    }

    [Fact]
    public async Task Avant_le_dernier_essai_un_echec_de_ffmpeg_laisse_le_decoupage_en_cours()
    {
        Recorded();
        await RequestAsync(new AudioSplitPartCommand(0, 60_000, "A", null));
        _processor.Failure = new AudioProcessingException("Fichier abîmé.");

        await Assert.ThrowsAsync<AudioProcessingException>(() => RunAsync(attempt: 2));

        Assert.Equal(AudioSplitStatus.Pending, _splits.Splits.Single().Status);
    }

    [Fact]
    public async Task Au_dernier_essai_l_echec_et_sa_raison_sont_visibles()
    {
        Recorded();
        await RequestAsync(new AudioSplitPartCommand(0, 60_000, "A", null));
        _processor.Failure = new AudioProcessingException("Fichier abîmé.", "Invalid data");

        await Assert.ThrowsAsync<AudioProcessingException>(() => RunAsync(attempt: 3));

        Assert.Equal("Fichier abîmé.", _splits.Splits.Single().Error);
    }

    [Fact]
    public async Task Un_passage_vide_fait_echouer_le_decoupage()
    {
        Recorded();
        await RequestAsync(new AudioSplitPartCommand(0, 60_000, "A", null), new(60_000, 120_000, "B", null));
        _processor.EmptyCut = 1;

        await Assert.ThrowsAsync<AudioProcessingException>(() => RunAsync(attempt: 3));

        Assert.Equal(AudioSplitStatus.Failed, _splits.Splits.Single().Status);
        Assert.Empty(_songs.Songs);
    }

    [Fact]
    public async Task Une_sauvegarde_en_conflit_efface_les_passages_stockes_et_sera_reessayee()
    {
        Recorded();
        await RequestAsync(new AudioSplitPartCommand(0, 60_000, "A", null));
        _uow.FailuresToSimulate = 1;

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => RunAsync());

        Assert.Single(_storage.Files);                                     // seul l'original reste
        Assert.NotEqual(AudioSplitStatus.Failed, _splits.Splits.Single().Status);
    }

    [Fact]
    public async Task Un_original_purge_entre_temps_fait_echouer_le_decoupage()
    {
        Recorded();
        await RequestAsync(new AudioSplitPartCommand(0, 60_000, "A", null));
        _previews.Sources.Clear();

        await RunAsync();

        Assert.Equal(AudioSplitStatus.Failed, _splits.Splits.Single().Status);
        Assert.Equal(0, _processor.Calls);
    }
}
