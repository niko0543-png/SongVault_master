using SongVault.Application.Abstractions;
using SongVault.Application.Files;
using SongVault.Application.Tests.Fakes;
using SongVault.Domain.Files;
using SongVault.Domain.Audit;

namespace SongVault.Application.Tests.Files;

[Trait("Category", "Security")]
public sealed class FileScanJobTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);
    private readonly FakeFileScanRepository _scans = new();
    private readonly FakeAudioPreviewRepository _previews = new();
    private readonly FakeFileStorageService _storage = new();
    private readonly FakeMalwareScanner _scanner = new();
    private readonly FakeBackgroundJobs _jobs = new();
    private readonly FakeUnitOfWork _uow = new();

    private static readonly Guid BandId = Guid.NewGuid();
    private readonly FakeAuditRepository _audit = new();
    private FileScanJob Sut() => new(_scans, _previews, _storage, _scanner, _jobs, _uow, new FixedTimeProvider(Now), _audit);

    /// <summary>Un fichier envoyé : son original dans le stockage, son analyse en attente et, s'il est audio, son aperçu.</summary>
    private Guid Uploaded(SongFileType type = SongFileType.Audio)
    {
        var fileId = Guid.NewGuid();
        _storage.Files["k.wav"] = [1, 2, 3];
        _scans.Targets[fileId] = new FileScanTarget("k.wav", type, BandId, "Nocturne — v1 — mix.wav");
        _scans.Add(FileScan.Request(fileId, Now));
        if (type == SongFileType.Audio) _previews.Add(AudioPreview.Request(fileId, Now));
        return fileId;
    }

    private static BackgroundJobContext Job(Guid fileId, int attempt = 1)
        => new(Guid.NewGuid(), fileId.ToString(), attempt, IsLastAttempt: attempt == 3);

    [Fact]
    public async Task Un_fichier_sain_devient_lisible_et_son_apercu_est_lance()
    {
        var fileId = Uploaded();

        await Sut().RunAsync(Job(fileId), CancellationToken.None);

        Assert.Equal(FileScanStatus.Clean, Assert.Single(_scans.Scans).Status);
        Assert.Empty(_audit.Entries);                            // #19 : un fichier sain n'entre pas au journal
        Assert.Equal((AudioPreviewJob.JobType, fileId.ToString(), Now), Assert.Single(_jobs.Enqueued));
        Assert.Equal(1, _scanner.Calls);
        Assert.Equal(1, _uow.SaveCount);
    }

    [Fact]
    public async Task Un_document_sain_n_a_pas_d_apercu_a_lancer()
    {
        var fileId = Uploaded(SongFileType.Document);

        await Sut().RunAsync(Job(fileId), CancellationToken.None);

        Assert.Equal(FileScanStatus.Clean, Assert.Single(_scans.Scans).Status);
        Assert.Empty(_jobs.Enqueued);
    }

    [Fact]
    public async Task Un_fichier_infecte_reste_en_quarantaine_et_son_auteur_est_prevenu()
    {
        var fileId = Uploaded();
        _scanner.Threat = "Win.Test.EICAR_HDB-1";

        await Sut().RunAsync(Job(fileId), CancellationToken.None);

        var scan = Assert.Single(_scans.Scans);
        Assert.Equal(FileScanStatus.Infected, scan.Status);
        Assert.Equal("Win.Test.EICAR_HDB-1", scan.Threat);
        Assert.Equal(AudioPreviewStatus.Failed, Assert.Single(_previews.Previews).Status);    // jamais converti
        Assert.Equal((InfectedFileNoticeJob.JobType, fileId.ToString(), Now), Assert.Single(_jobs.Enqueued));
        Assert.Equal([1, 2, 3], _storage.Files["k.wav"]);        // quarantaine : l'original n'est ni déplacé ni effacé
                                                                 // #19 : au journal d'audit du groupe, sans auteur
        var entry = Assert.Single(_audit.Entries);
        Assert.Equal((BandId, (string?)null, AuditAction.FileQuarantined), (entry.BandId, entry.ActorId, entry.Action));
        Assert.Equal("Nocturne — v1 — mix.wav", entry.Subject);
        Assert.Equal("menace : Win.Test.EICAR_HDB-1", entry.Details);
    }

    [Fact]
    public async Task Un_fichier_deja_analyse_n_est_pas_analyse_de_nouveau()
    {
        var fileId = Uploaded();
        _scans.Scans.Single().MarkClean(Now);

        await Sut().RunAsync(Job(fileId), CancellationToken.None);

        Assert.Equal(0, _scanner.Calls);
        Assert.Equal(0, _uow.SaveCount);
    }

    [Fact]
    public async Task Un_fichier_supprime_pendant_l_analyse_ne_fait_pas_echouer_la_tache()
    {
        var fileId = Uploaded();
        _uow.FailuresToSimulate = 1;                            // la ligne de l'analyse est partie avec le fichier

        await Sut().RunAsync(Job(fileId), CancellationToken.None);

        Assert.Equal(1, _uow.SaveCount);
    }

    [Fact]
    public async Task Antivirus_injoignable_l_essai_echoue_et_le_fichier_reste_en_attente()
    {
        var fileId = Uploaded();
        _scanner.Failure = new MalwareScannerException("ClamAV injoignable (localhost:3310).");

        await Assert.ThrowsAsync<MalwareScannerException>(() => Sut().RunAsync(Job(fileId, attempt: 1), CancellationToken.None));

        Assert.Equal(FileScanStatus.Pending, Assert.Single(_scans.Scans).Status);
        Assert.Empty(_jobs.Enqueued);
    }

    [Fact]
    public async Task Au_dernier_essai_l_analyse_passe_en_echec_avec_une_raison_lisible()
    {
        var fileId = Uploaded();
        _scanner.Failure = new MalwareScannerException("ClamAV injoignable (localhost:3310).");

        await Assert.ThrowsAsync<MalwareScannerException>(() => Sut().RunAsync(Job(fileId, attempt: 3), CancellationToken.None));

        var scan = Assert.Single(_scans.Scans);
        Assert.Equal(FileScanStatus.Failed, scan.Status);
        Assert.Equal("L'antivirus n'a pas pu analyser le fichier.", scan.Error);
        Assert.Equal(1, _uow.DiscardCount);
        Assert.Equal(AudioPreviewStatus.Pending, Assert.Single(_previews.Previews).Status);   // relancé avec l'analyse
    }

    [Fact]
    public async Task Antivirus_desactive_le_fichier_devient_lisible_sans_etre_lu()
    {
        var fileId = Uploaded();
        _scanner.IsEnabled = false;
        _storage.Files.Clear();                                  // rien n'est lu : même un original absent passe

        await Sut().RunAsync(Job(fileId), CancellationToken.None);

        Assert.Equal(FileScanStatus.NotScanned, Assert.Single(_scans.Scans).Status);
        Assert.Equal((AudioPreviewJob.JobType, fileId.ToString(), Now), Assert.Single(_jobs.Enqueued));
        Assert.Equal(0, _scanner.Calls);
        Assert.Equal(1, _uow.SaveCount);
    }
}