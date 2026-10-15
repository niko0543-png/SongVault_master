using SongVault.Application.Common.Exceptions;
using SongVault.Application.Files;
using SongVault.Application.Tests.Fakes;
using SongVault.Domain.Files;
using SongVault.Domain.Songs;

namespace SongVault.Application.Tests.Files;

public sealed class RetryAudioPreviewHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 9, 0, 0, TimeSpan.Zero);
    private readonly FakeSongRepository _songs = new();
    private readonly FakeAudioPreviewRepository _previews = new();
    private readonly FakeBackgroundJobs _jobs = new();
    private readonly FakeUnitOfWork _uow = new();

    private RetryAudioPreviewHandler Sut() => new(_songs, _previews, _jobs, _uow, new FixedTimeProvider(Now));

    private (Guid SongId, Guid VersionId, SongFile File) Uploaded(SongFileType type = SongFileType.Audio)
    {
        var song = Song.Create(Guid.NewGuid(), "Nocturne", null, null, Now);
        var version = song.AddVersion("v1", SongVersionStatus.Demo, null, null, Now);
        var file = version.AddFile("studio.wav", "k.wav", "audio/wav", 10, type, Now);
        _songs.Songs.Add(song);
        return (song.Id, version.Id, file);
    }

    [Fact]
    public async Task Relance_un_apercu_en_echec()
    {
        var (songId, versionId, file) = Uploaded();
        var preview = AudioPreview.Request(file.Id, Now.AddDays(-1));
        preview.MarkFailed("Fichier abîmé.", Now.AddDays(-1));
        _previews.Add(preview);

        var status = await Sut().HandleAsync(songId, versionId, file.Id, CancellationToken.None);

        Assert.Equal(AudioPreviewStatus.Pending, status);
        Assert.Equal(AudioPreviewStatus.Pending, preview.Status);
        Assert.Equal((AudioPreviewJob.JobType, file.Id.ToString(), Now), Assert.Single(_jobs.Enqueued));
        Assert.Equal(1, _uow.SaveCount);
    }

    [Fact]
    public async Task Refuse_de_relancer_un_apercu_deja_en_preparation()
    {
        var (songId, versionId, file) = Uploaded();
        _previews.Add(AudioPreview.Request(file.Id, Now));

        var error = await Assert.ThrowsAsync<ConflictException>(() => Sut().HandleAsync(songId, versionId, file.Id, CancellationToken.None));

        Assert.Contains("déjà en préparation", error.Message);
        Assert.Empty(_jobs.Enqueued);
    }

    [Fact]
    public async Task Un_document_n_a_pas_d_apercu_a_relancer()
    {
        var (songId, versionId, file) = Uploaded(SongFileType.Document);

        await Assert.ThrowsAsync<NotFoundException>(() => Sut().HandleAsync(songId, versionId, file.Id, CancellationToken.None));
    }
}