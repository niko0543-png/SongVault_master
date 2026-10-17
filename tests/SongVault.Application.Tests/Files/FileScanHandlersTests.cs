using Microsoft.Extensions.Options;

using SongVault.Application.Abstractions;
using SongVault.Application.Common;
using SongVault.Application.Common.Exceptions;
using SongVault.Application.Files;
using SongVault.Application.Tests.Fakes;
using SongVault.Domain.Files;
using SongVault.Domain.Songs;

namespace SongVault.Application.Tests.Files;

[Trait("Category", "Security")]
public sealed class FileScanHandlersTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);
    private readonly FakeSongRepository _songs = new();
    private readonly FakeFileScanRepository _scans = new();
    private readonly FakeFileStorageService _storage = new();
    private readonly FakeBackgroundJobs _jobs = new();
    private readonly FakeUnitOfWork _uow = new();

    private (Guid SongId, Guid VersionId, SongFile File, FileScan Scan) Uploaded()
    {
        var song = Song.Create(Guid.NewGuid(), "Nocturne", null, null, Now);
        var version = song.AddVersion("v1", SongVersionStatus.Demo, null, null, Now);
        var file = version.AddFile("paroles.pdf", "k.pdf", "application/pdf", 3, SongFileType.Document, Now);
        _songs.Songs.Add(song);
        _storage.Files["k.pdf"] = [1, 2, 3];
        var scan = FileScan.Request(file.Id, Now);
        _scans.Add(scan);
        return (song.Id, version.Id, file, scan);
    }

    // ---------- Téléchargement ----------
    [Theory]
    [InlineData(FileScanStatus.Pending, "Analyse antivirus en cours")]
    [InlineData(FileScanStatus.Infected, "Fichier bloqué")]
    [InlineData(FileScanStatus.Failed, "relancez-la")]
    public async Task Un_fichier_pas_declare_sain_ne_se_telecharge_pas(FileScanStatus status, string message)
    {
        var (songId, versionId, file, _) = Uploaded();
        _songs.Scans[file.Id] = status;

        var error = await Assert.ThrowsAsync<FileNotReadableException>(() =>
            new DownloadSongFileHandler(_songs, _storage).HandleAsync(songId, versionId, file.Id, true, CancellationToken.None));

        Assert.Contains(message, error.Message);
    }

    [Fact]
    public void Un_fichier_non_analyse_antivirus_desactive_est_lisible()
        => FileScanGate.EnsureReadable(FileScanStatus.NotScanned);

    [Fact]
    public void Un_fichier_sans_analyse_connue_est_traite_comme_en_cours()
        => Assert.Contains("en cours", Assert.Throws<FileNotReadableException>(() => FileScanGate.EnsureReadable(null)).Message);

    [Fact]
    public async Task Un_fichier_sain_se_telecharge()
    {
        var (songId, versionId, file, _) = Uploaded();
        _songs.Scans[file.Id] = FileScanStatus.Clean;

        var download = await new DownloadSongFileHandler(_songs, _storage)
            .HandleAsync(songId, versionId, file.Id, true, CancellationToken.None);

        Assert.Equal(3, download.Content!.Length);
    }

    // ---------- Relance ----------
    private RetryFileScanHandler Retry() => new(_songs, _scans, _jobs, _uow, new FixedTimeProvider(Now));

    [Fact]
    public async Task Relance_une_analyse_en_echec()
    {
        var (songId, versionId, file, scan) = Uploaded();
        scan.MarkFailed("Antivirus injoignable.", Now);

        var status = await Retry().HandleAsync(songId, versionId, file.Id, CancellationToken.None);

        Assert.Equal(FileScanStatus.Pending, status);
        Assert.Equal(FileScanStatus.Pending, scan.Status);
        Assert.Equal((FileScanJob.JobType, file.Id.ToString(), Now), Assert.Single(_jobs.Enqueued));
        Assert.Equal(1, _uow.SaveCount);
    }

    [Fact]
    public async Task Ne_relance_ni_une_analyse_en_cours_ni_un_fichier_infecte()
    {
        var (songId, versionId, file, scan) = Uploaded();
        var pending = await Assert.ThrowsAsync<ConflictException>(() => Retry().HandleAsync(songId, versionId, file.Id, CancellationToken.None));
        Assert.Contains("déjà en cours", pending.Message);

        scan.MarkInfected("Win.Test.EICAR_HDB-1", Now);
        var infected = await Assert.ThrowsAsync<ConflictException>(() => Retry().HandleAsync(songId, versionId, file.Id, CancellationToken.None));
        Assert.Contains("supprimez-le", infected.Message);

        Assert.Empty(_jobs.Enqueued);
    }

    [Fact]
    public async Task Un_fichier_non_analyse_ne_se_relance_pas_a_la_main()
    {
        var (songId, versionId, file, scan) = Uploaded();
        scan.MarkNotScanned(Now);

        var error = await Assert.ThrowsAsync<ConflictException>(() => Retry().HandleAsync(songId, versionId, file.Id, CancellationToken.None));

        Assert.Contains("pas encore activé", error.Message);
        Assert.Empty(_jobs.Enqueued);
    }

    // ---------- Activation de l'antivirus ----------
    [Fact]
    public async Task A_l_activation_les_fichiers_non_analyses_sont_remis_en_analyse_par_lots()
    {
        var clean = FileScan.Request(Guid.NewGuid(), Now);
        clean.MarkClean(Now);
        _scans.Add(clean);
        var notScanned = Enumerable.Range(0, RescanNotScannedFilesHandler.BatchSize + 1).Select(_ =>
        {
            var scan = FileScan.Request(Guid.NewGuid(), Now);
            scan.MarkNotScanned(Now);
            _scans.Add(scan);
            return scan;
        }).ToList();

        var count = await new RescanNotScannedFilesHandler(_scans, _jobs, _uow, new FixedTimeProvider(Now))
            .HandleAsync(CancellationToken.None);

        Assert.Equal(RescanNotScannedFilesHandler.BatchSize + 1, count);
        Assert.All(notScanned, s => Assert.Equal(FileScanStatus.Pending, s.Status));
        Assert.Equal(FileScanStatus.Clean, clean.Status);                     // déjà analysé : pas repris
        Assert.Equal(count, _jobs.Enqueued.Count(j => j.Type == FileScanJob.JobType));
        Assert.Equal(2, _uow.SaveCount);                                        // deux lots
    }

    [Fact]
    public async Task Relancer_le_fichier_d_un_autre_groupe_est_introuvable()
        => await Assert.ThrowsAsync<NotFoundException>(() =>
            Retry().HandleAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None));

    // ---------- Avertissement ----------
    [Fact]
    public async Task L_auteur_d_un_fichier_infecte_recoit_un_e_mail_avec_le_lien_de_la_version()
    {
        var emails = new FakeEmailSender();
        var notice = new InfectedFileNotice("ana@test.local", Guid.NewGuid(), "Les Nocturnes", Guid.NewGuid(), "Nocturne",
            Guid.NewGuid(), 4, "facture<script>.pdf", "Win.Test.EICAR_HDB-1");
        var fileId = Guid.NewGuid();
        _scans.Notices[fileId] = notice;
        var job = new InfectedFileNoticeJob(_scans, emails, Options.Create(new AppOptions { PublicUrl = "https://songvault.example/" }));

        await job.RunAsync(new BackgroundJobContext(Guid.NewGuid(), fileId.ToString(), 1, false), CancellationToken.None);

        var email = Assert.Single(emails.Sent);
        Assert.Equal("ana@test.local", email.To);
        Assert.Contains("Win.Test.EICAR_HDB-1", email.TextBody);
        Assert.Contains($"https://songvault.example/b/{notice.BandId}/songs/{notice.SongId}/versions/{notice.VersionId}", email.TextBody);
        Assert.Contains("facture&lt;script&gt;.pdf", email.HtmlBody);            // nom de fichier échappé
    }

    [Fact]
    public async Task Sans_auteur_connu_personne_n_est_prevenu()
    {
        var emails = new FakeEmailSender();
        var job = new InfectedFileNoticeJob(_scans, emails, Options.Create(new AppOptions { PublicUrl = "https://songvault.example" }));

        await job.RunAsync(new BackgroundJobContext(Guid.NewGuid(), Guid.NewGuid().ToString(), 1, false), CancellationToken.None);

        Assert.Empty(emails.Sent);
    }
}