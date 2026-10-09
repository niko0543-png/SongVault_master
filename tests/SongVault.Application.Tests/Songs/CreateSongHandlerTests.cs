using SongVault.Application.Songs.CreateSong;
using SongVault.Application.Tests.Fakes;
using SongVault.Domain.Common;

namespace SongVault.Application.Tests.Songs;

public sealed class CreateSongHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid BandId = Guid.CreateVersion7();
    private readonly FakeSongRepository _songs = new();
    private readonly FakeUnitOfWork _uow = new();

    private CreateSongHandler CreateSut() => new(_songs, _uow, new FakeBandContext(BandId), new FixedTimeProvider(Now));

    [Fact]
    public async Task Ajoute_le_morceau_au_groupe_actif_et_enregistre_une_seule_fois()
    {
        var result = await CreateSut().HandleAsync(
            new CreateSongCommand("Nocturne", "Band", null), CancellationToken.None);

        var stored = Assert.Single(_songs.Songs);
        Assert.Equal(stored.Id, result.Id);
        Assert.Equal(Now, result.CreatedAt);
        Assert.Equal(1, _uow.SaveCount);
        Assert.Equal(BandId, stored.BandId);
    }

    [Fact]
    public async Task Titre_invalide_ne_persiste_rien()
    {
        await Assert.ThrowsAsync<DomainException>(() => CreateSut().HandleAsync(
            new CreateSongCommand("   ", null, null), CancellationToken.None));

        Assert.Empty(_songs.Songs);
        Assert.Equal(0, _uow.SaveCount);
    }
}