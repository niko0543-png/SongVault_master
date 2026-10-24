using SongVault.Application.Abstractions;
using SongVault.Application.Common.Exceptions;
using SongVault.Application.Files;
using SongVault.Application.Tests.Fakes;
using SongVault.Domain.Files;
using SongVault.Domain.Songs;

namespace SongVault.Application.Tests.Files;

public sealed class AudioPreviewJobTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 9, 0, 0, TimeSpan.Zero);
    private readonly FakeAudioPreviewRepository _previews = new();
    private readonly FakeFileStorageService _storage = new();
    private readonly FakeAudioProcessor _processor = new();
    private readonly FakeUnitOfWork _uow = new();

    private AudioPreviewJob Sut() => new(_previews, _storage, _processor, _uow, new FixedTimeProvider(Now));

    /// <summary>Un fichier envoyé, son original dans le stockage et son aperçu en préparation.</summary>
    private Guid Uploaded(string storageKey)
    {
        var fileId = Guid.NewGuid();
        _storage.Files[storageKey] = [1, 2, 3];
        _previews.Sources[fileId] = storageKey;
        _previews.Add(AudioPreview.Request(fileId, Now));
        return fileId;
    }

    private static BackgroundJobContext Job(Guid fileId, int attempt = 1)
        => new(Guid.NewGuid(), fileId.ToString(), attempt, IsLastAttempt: attempt == 3);

    [Fact]
    public async Task Un_wav_donne_un_mp3_d_ecoute_et_l_original_reste_intact()
    {
        var fileId = Uploaded("studio.wav");

        await Sut().RunAsync(Job(fileId), CancellationToken.None);

        var preview = Assert.Single(_previews.Previews);
        Assert.Equal(AudioPreviewStatus.Ready, preview.Status);
        Assert.Equal(AudioPreview.StorageKeyFor(fileId), preview.PreviewStorageKey);
        Assert.Equal("MP3"u8.ToArray(), _storage.Files[preview.PreviewStorageKey!]);
        Assert.Equal([1, 2, 3], _storage.Files["studio.wav"]);
        Assert.Equal(183_000, preview.DurationMs);
        Assert.Equal(1, _uow.SaveCount);
    }

    [Fact]
    public async Task Un_mp3_garde_l_original_pour_l_ecoute()
    {
        var fileId = Uploaded("maquette.MP3");

        await Sut().RunAsync(Job(fileId), CancellationToken.None);

        var preview = Assert.Single(_previews.Previews);
        Assert.Equal(AudioPreviewStatus.Ready, preview.Status);
        Assert.Null(preview.PreviewStorageKey);
        Assert.Single(_storage.Files);                  // aucun MP3 de plus
    }

    [Fact]
    public async Task Un_m4a_est_copie_sur_le_disque_le_temps_de_la_conversion()
    {
        var fileId = Uploaded("repetition.m4a");

        await Sut().RunAsync(Job(fileId), CancellationToken.None);

        // ADR 0049 : ffmpeg doit pouvoir aller lire l'index rangé à la fin ; la copie disparaît ensuite
        Assert.NotNull(_processor.SourcePath);
        Assert.False(File.Exists(_processor.SourcePath));
        Assert.Equal(AudioPreviewStatus.Ready, Assert.Single(_previews.Previews).Status);
    }

    [Fact]
    public async Task Un_webm_est_lu_au_fil_du_stockage()
    {
        var fileId = Uploaded("repetition.webm");

        await Sut().RunAsync(Job(fileId), CancellationToken.None);

        Assert.Null(_processor.SourcePath);
        Assert.Equal(AudioPreviewStatus.Ready, Assert.Single(_previews.Previews).Status);
    }

    [Fact]
    public async Task Avant_le_dernier_essai_un_echec_laisse_l_apercu_en_preparation()
    {
        var fileId = Uploaded("studio.flac");
        _processor.Failure = new AudioProcessingException("Fichier abîmé.");

        await Assert.ThrowsAsync<AudioProcessingException>(() => Sut().RunAsync(Job(fileId, attempt: 2), CancellationToken.None));

        Assert.Equal(AudioPreviewStatus.Pending, Assert.Single(_previews.Previews).Status);
    }

    [Fact]
    public async Task Au_dernier_essai_l_echec_et_sa_raison_sont_visibles()
    {
        var fileId = Uploaded("studio.flac");
        _processor.Failure = new AudioProcessingException("Fichier abîmé.", "Invalid data found when processing input");

        await Assert.ThrowsAsync<AudioProcessingException>(() => Sut().RunAsync(Job(fileId, attempt: 3), CancellationToken.None));

        var preview = Assert.Single(_previews.Previews);
        Assert.Equal(AudioPreviewStatus.Failed, preview.Status);
        Assert.Equal("Fichier abîmé.", preview.Error);          // pas le détail technique de ffmpeg
        Assert.Equal(1, _uow.DiscardCount);
    }

    [Fact]
    public async Task Une_erreur_technique_n_est_pas_montree_telle_quelle()
    {
        var fileId = Uploaded("studio.wav");
        _processor.Failure = new InvalidOperationException("ffmpeg introuvable (« ffmpeg »)");

        await Assert.ThrowsAsync<InvalidOperationException>(() => Sut().RunAsync(Job(fileId, attempt: 3), CancellationToken.None));

        Assert.Equal("Erreur technique pendant la conversion.", Assert.Single(_previews.Previews).Error);
    }

    [Fact]
    public async Task Un_fichier_supprime_pendant_la_conversion_ne_laisse_pas_de_mp3()
    {
        var fileId = Uploaded("studio.wav");
        _uow.FailuresToSimulate = 1;                            // la ligne de l'aperçu est partie avec le fichier

        await Sut().RunAsync(Job(fileId), CancellationToken.None);

        Assert.False(_storage.Files.ContainsKey(AudioPreview.StorageKeyFor(fileId)));
    }

    [Fact]
    public async Task Ne_refait_pas_un_apercu_deja_pret()
    {
        var fileId = Uploaded("studio.wav");
        await Sut().RunAsync(Job(fileId), CancellationToken.None);

        await Sut().RunAsync(Job(fileId), CancellationToken.None);

        Assert.Equal(1, _processor.Calls);
    }
}