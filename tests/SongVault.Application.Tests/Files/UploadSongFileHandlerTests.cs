using SongVault.Application.Common.Exceptions;
using SongVault.Application.Files;
using SongVault.Application.Tests.Fakes;
using SongVault.Domain.Songs;

namespace SongVault.Application.Tests.Files;

[Trait("Category", "Security")]
public sealed class UploadSongFileHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero);
    private readonly FakeSongRepository _songs = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeFileStorageService _storage = new();

    private UploadSongFileHandler Sut() => new(_songs, _uow, _storage, new FixedTimeProvider(Now));

    private UploadSongFileCommand Command(long length, byte[] content, string fileName = "maquette.mp3")
    {
        var song = Song.Create("Nocturne", null, null, Now);
        var version = song.AddVersion("v1", SongVersionStatus.Demo, null, null, Now);
        _songs.Songs.Add(song);
        return new UploadSongFileCommand(song.Id, version.Id, fileName, length, new MemoryStream(content));
    }

    [Fact]
    public async Task Refuse_un_fichier_de_50_Mo_plus_1_octet_sans_rien_stocker()
    {
        var command = Command(FileTypePolicy.MaxFileSizeBytes + 1, "ID3"u8.ToArray());

        await Assert.ThrowsAsync<FileTooLargeException>(() => Sut().HandleAsync(command, CancellationToken.None));

        Assert.Empty(_storage.Files);
        Assert.Equal(0, _uow.SaveCount);
    }

    [Fact]
    public async Task Refuse_un_exe_renomme_sans_rien_stocker()
    {
        var command = Command(4, [0x4D, 0x5A, 0x90, 0x00]);

        var error = await Assert.ThrowsAsync<UnsupportedFileException>(() => Sut().HandleAsync(command, CancellationToken.None));

        Assert.Contains("ne correspond pas", error.Message);
        Assert.Empty(_storage.Files);
    }

    [Fact]
    public async Task Stocke_un_vrai_mp3_avec_son_en_tete_intact()
    {
        byte[] mp3 = [.. "ID3"u8, 0x04, 0x00, .. new byte[100]];
        var command = Command(mp3.Length, mp3);

        await Sut().HandleAsync(command, CancellationToken.None);

        var stored = Assert.Single(_storage.Files).Value;
        Assert.Equal(mp3, stored);       // le flux a bien été remis au début avant l'enregistrement
    }
}