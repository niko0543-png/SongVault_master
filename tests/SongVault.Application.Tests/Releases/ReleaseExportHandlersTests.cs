using System.IO.Compression;

using Microsoft.Extensions.Options;

using SongVault.Application.Abstractions;
using SongVault.Application.Common;
using SongVault.Application.Common.Exceptions;
using SongVault.Application.Releases.Exports;
using SongVault.Application.Tests.Fakes;
using SongVault.Domain.Files;
using SongVault.Domain.Releases;

namespace SongVault.Application.Tests.Releases;

public sealed class ReleaseExportHandlersTests
{
    private static readonly DateTimeOffset Now = ReleaseExportTestData.Now;
    private readonly ReleaseExportTestData _data = new();

    private RequestReleaseExportHandler Request(DateTimeOffset? at = null)
        => new(_data.Exports, _data.Jobs, _data.UnitOfWork, new FakeBandContext(ReleaseExportTestData.BandId),
            new FakeCurrentUser("member-1"), new FixedTimeProvider(at ?? Now));

    private ExportReleaseJob Job(DateTimeOffset? at = null)
        => new(_data.Exports, _data.Storage, _data.Jobs, _data.UnitOfWork, _data.Notifications, _data.Emails,
            Options.Create(new AppOptions { PublicUrl = "https://songvault.example/" }), new FixedTimeProvider(at ?? Now));

    private DownloadReleaseExportHandler Download(DateTimeOffset at, Guid? bandId = null)
        => new(_data.Exports, _data.Storage, new FakeBandContext(bandId ?? ReleaseExportTestData.BandId), new FixedTimeProvider(at));

    private static BackgroundJobContext Context(Guid exportId, bool last = false) => new(Guid.NewGuid(), exportId.ToString(), 1, last);

    private async Task<ReleaseExport> ReadyExportAsync()
    {
        var dto = await Request().HandleAsync(_data.Release.Id, CancellationToken.None);
        await Job().RunAsync(Context(dto.Id), CancellationToken.None);
        return _data.Exports.Exports.Single();
    }

    [Fact]
    public async Task La_demande_enregistre_l_archive_et_sa_tache_ensemble()
    {
        var dto = await Request().HandleAsync(_data.Release.Id, CancellationToken.None);

        Assert.Equal(ReleaseExportStatus.Pending, dto.Status);
        Assert.Equal("member-1", _data.Exports.Exports.Single().RequestedById);
        Assert.Equal((ExportReleaseJob.JobType, dto.Id.ToString(), Now), _data.Jobs.Enqueued.Single());
        Assert.Equal(1, _data.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task Une_seule_archive_en_preparation_par_sortie()
    {
        await Request().HandleAsync(_data.Release.Id, CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() => Request().HandleAsync(_data.Release.Id, CancellationToken.None));
    }

    [Fact]
    public async Task La_sortie_d_un_autre_groupe_est_introuvable()
    {
        var handler = new RequestReleaseExportHandler(_data.Exports, _data.Jobs, _data.UnitOfWork,
            new FakeBandContext(Guid.CreateVersion7()), new FakeCurrentUser(), new FixedTimeProvider(Now));

        await Assert.ThrowsAsync<NotFoundException>(() => handler.HandleAsync(_data.Release.Id, CancellationToken.None));
    }

    [Fact]
    public async Task La_tache_ecrit_le_zip_programme_son_expiration_et_previent_le_demandeur()
    {
        var export = await ReadyExportAsync();

        Assert.Equal(ReleaseExportStatus.Ready, export.Status);
        Assert.Equal(Now.AddHours(24), export.ExpiresAt);
        var bytes = _data.Storage.Files[export.StorageKey];
        Assert.Equal(bytes.Length, export.SizeBytes);
        using var zip = new ZipArchive(new MemoryStream(bytes));
        Assert.Contains(zip.Entries, e => e.FullName == "Nuits blanches/credits.csv");
        Assert.Contains((ExpireReleaseExportJob.JobType, export.Id.ToString(), Now.AddHours(24)), _data.Jobs.Enqueued);

        var email = Assert.Single(_data.Emails.Sent);
        Assert.Equal("member-1@test.local", email.To);
        Assert.Contains($"https://songvault.example/b/{ReleaseExportTestData.BandId}/releases/{_data.Release.Id}", email.TextBody);
        Assert.Contains("20 octobre 2026 à 11h00", email.TextBody);                // 9 h UTC + 24 h, heure d'été de Paris
        Assert.DoesNotContain(export.StorageKey, email.TextBody);                   // pas de lien direct vers l'archive
    }

    [Fact]
    public async Task Une_adresse_refusee_ne_fait_pas_echouer_la_tache()
    {
        _data.Emails.FailFor.Add("member-1@test.local");

        var export = await ReadyExportAsync();

        Assert.Equal(ReleaseExportStatus.Ready, export.Status);
    }

    [Fact]
    public async Task Un_fichier_ajoute_entre_temps_et_pas_encore_analyse_fait_echouer_le_dernier_essai_sans_laisser_d_archive()
    {
        var dto = await Request().HandleAsync(_data.Release.Id, CancellationToken.None);
        _data.AddAudio(_data.First, "prise 2.wav", [1], FileScanStatus.Pending);

        await Assert.ThrowsAsync<ConflictException>(() => Job().RunAsync(Context(dto.Id, last: true), CancellationToken.None));

        var export = _data.Exports.Exports.Single();
        Assert.Equal(ReleaseExportStatus.Failed, export.Status);
        Assert.False(_data.Storage.Files.ContainsKey(export.StorageKey));
        Assert.Contains((ExpireReleaseExportJob.JobType, dto.Id.ToString(), Now.AddHours(24)), _data.Jobs.Enqueued);
        Assert.Empty(_data.Emails.Sent);
    }

    [Fact]
    public async Task Un_essai_rate_efface_l_archive_partielle_et_le_suivant_la_reecrit()
    {
        var dto = await Request().HandleAsync(_data.Release.Id, CancellationToken.None);
        var key = ReleaseExport.StorageKeyFor(dto.Id);
        var cover = _data.Storage.Files["k-cover"];
        _data.Storage.Files.Remove("k-cover");                                  // la lecture de la pochette échoue

        await Assert.ThrowsAsync<FileNotFoundException>(() => Job().RunAsync(Context(dto.Id), CancellationToken.None));
        Assert.False(_data.Storage.Files.ContainsKey(key));
        Assert.Equal(ReleaseExportStatus.Pending, _data.Exports.Exports.Single().Status);

        _data.Storage.Files["k-cover"] = cover;
        await Job().RunAsync(Context(dto.Id), CancellationToken.None);
        Assert.True(_data.Storage.Files.ContainsKey(key));
    }

    [Fact]
    public async Task L_expiration_efface_la_ligne_et_le_fichier_meme_sans_ligne()
    {
        var export = await ReadyExportAsync();
        var expire = new ExpireReleaseExportJob(_data.Exports, _data.Storage, _data.UnitOfWork);

        _data.Exports.Exports.Clear();                                          // partie avec sa sortie
        await expire.RunAsync(Context(export.Id), CancellationToken.None);

        Assert.False(_data.Storage.Files.ContainsKey(export.StorageKey));
    }

    [Fact]
    public async Task Le_telechargement_redirige_vers_une_adresse_courte_du_stockage_objet()
    {
        var export = await ReadyExportAsync();
        _data.Storage.BaseUrl = new Uri("https://blob.example/");

        var download = await Download(Now.AddHours(1)).HandleAsync(_data.Release.Id, export.Id, CancellationToken.None);

        Assert.Equal(new Uri($"https://blob.example/{export.StorageKey}"), download.RedirectUrl);
        Assert.Equal("Nuits blanches.zip", download.FileName);
    }

    [Fact]
    public async Task Un_lien_expire_est_refuse_avec_410()
    {
        var export = await ReadyExportAsync();

        var error = await Assert.ThrowsAsync<LinkExpiredException>(
            () => Download(Now.AddHours(24)).HandleAsync(_data.Release.Id, export.Id, CancellationToken.None));
        Assert.Contains("expiré", error.Message);
        Assert.Null(await new GetLatestReleaseExportHandler(_data.Exports, new FakeBandContext(ReleaseExportTestData.BandId),
            new FixedTimeProvider(Now.AddHours(24))).HandleAsync(_data.Release.Id, CancellationToken.None));
    }

    [Fact]
    public async Task L_archive_d_un_autre_groupe_ou_d_une_autre_sortie_est_introuvable()
    {
        var export = await ReadyExportAsync();

        await Assert.ThrowsAsync<NotFoundException>(
            () => Download(Now, Guid.CreateVersion7()).HandleAsync(_data.Release.Id, export.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(
            () => Download(Now).HandleAsync(Guid.CreateVersion7(), export.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Une_archive_en_preparation_ne_se_telecharge_pas()
    {
        var dto = await Request().HandleAsync(_data.Release.Id, CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(
            () => Download(Now).HandleAsync(_data.Release.Id, dto.Id, CancellationToken.None));
    }
}