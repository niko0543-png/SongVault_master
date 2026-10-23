using SongVault.Application.Common.Exceptions;
using SongVault.Application.Files.Sync;
using SongVault.Application.Tests.Fakes;
using SongVault.Domain.Common;
using SongVault.Domain.Files;
using SongVault.Domain.Songs;

namespace SongVault.Application.Tests.Files;

public sealed class TabSyncHandlersTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly TabSyncPoint[] Points = [new(0, 0, 0, 1000), new(1, 0, 0, 2500)];

    private readonly FakeSongRepository _songs = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FixedTimeProvider _clock = new(Now.AddHours(1));

    private (Song Song, SongVersion Version, SongFile Tab, SongFile Demo) ExistingVersion()
    {
        var song = Song.Create(Guid.CreateVersion7(), "Nocturne", null, null, Now);
        var version = song.AddVersion("Maquette", SongVersionStatus.Demo, null, null, Now);
        var tab = version.AddFile("riff.gp5", "k1", "application/octet-stream", 10, SongFileType.Tablature, Now);
        var demo = version.AddFile("maquette.wav", "k2", "audio/wav", 10, SongFileType.Audio, Now);
        _songs.Songs.Add(song);
        return (song, version, tab, demo);
    }

    private GetTabSyncHandler GetHandler() => new(_songs);
    private SetTabSyncHandler SetHandler() => new(_songs, _uow, _clock);
    private DeleteTabSyncHandler DeleteHandler() => new(_songs, _uow, _clock);

    [Fact]
    public async Task Set_enregistre_le_calage_et_le_rend_avec_le_nom_de_la_maquette()
    {
        var (song, version, tab, demo) = ExistingVersion();

        var sync = await SetHandler().HandleAsync(new SetTabSyncCommand(song.Id, version.Id, tab.Id, demo.Id, Points), default);

        Assert.Equal((demo.Id, "maquette.wav"), (sync.AudioFileId, sync.AudioFileName));
        Assert.Equal(Points, sync.Points);
        Assert.Equal(1, _uow.SaveCount);
        Assert.Equal(Now.AddHours(1), version.UpdatedAt);
    }

    [Fact]
    public async Task Set_laisse_passer_l_erreur_du_domaine_sans_enregistrer()
    {
        var (song, version, tab, demo) = ExistingVersion();

        await Assert.ThrowsAsync<DomainException>(() => SetHandler().HandleAsync(
            new SetTabSyncCommand(song.Id, version.Id, tab.Id, demo.Id, [new(0, 0, 0, 2000), new(1, 0, 0, 1000)]), default));

        Assert.Equal(0, _uow.SaveCount);
    }

    [Fact]
    public async Task Get_rend_null_sans_calage_puis_le_calage_enregistre()
    {
        var (song, version, tab, demo) = ExistingVersion();
        Assert.Null(await GetHandler().HandleAsync(song.Id, version.Id, tab.Id, default));

        version.SetTablatureSync(tab.Id, demo.Id, Points, Now);

        var sync = await GetHandler().HandleAsync(song.Id, version.Id, tab.Id, default);
        Assert.Equal(demo.Id, sync!.AudioFileId);
    }

    [Fact]
    public async Task Get_rend_null_quand_la_maquette_est_a_la_corbeille()
    {
        var (song, version, tab, demo) = ExistingVersion();
        version.SetTablatureSync(tab.Id, demo.Id, Points, Now);
        version.TrashFile(demo.Id, "ana", Now);

        Assert.Null(await GetHandler().HandleAsync(song.Id, version.Id, tab.Id, default));
    }

    [Fact]
    public async Task Un_fichier_qui_n_est_pas_une_tablature_ou_une_version_inconnue_rendent_404()
    {
        var (song, version, _, demo) = ExistingVersion();

        await Assert.ThrowsAsync<NotFoundException>(() => GetHandler().HandleAsync(song.Id, version.Id, demo.Id, default));
        await Assert.ThrowsAsync<NotFoundException>(() => GetHandler().HandleAsync(song.Id, Guid.CreateVersion7(), demo.Id, default));
        await Assert.ThrowsAsync<NotFoundException>(() => SetHandler().HandleAsync(
            new SetTabSyncCommand(song.Id, version.Id, demo.Id, demo.Id, Points), default));
    }

    [Fact]
    public async Task Get_rend_404_pour_une_version_a_la_corbeille()
    {
        var (song, version, tab, _) = ExistingVersion();
        song.TrashVersion(version.Id, "ana", Now);

        await Assert.ThrowsAsync<NotFoundException>(() => GetHandler().HandleAsync(song.Id, version.Id, tab.Id, default));
    }

    [Fact]
    public async Task Delete_retire_le_calage_et_n_enregistre_rien_s_il_n_y_en_avait_pas()
    {
        var (song, version, tab, demo) = ExistingVersion();
        await DeleteHandler().HandleAsync(song.Id, version.Id, tab.Id, default);
        Assert.Equal(0, _uow.SaveCount);

        version.SetTablatureSync(tab.Id, demo.Id, Points, Now);
        await DeleteHandler().HandleAsync(song.Id, version.Id, tab.Id, default);

        Assert.Null(tab.SyncPoints);
        Assert.Equal(1, _uow.SaveCount);
    }
}