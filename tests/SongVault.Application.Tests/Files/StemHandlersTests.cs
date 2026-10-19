using SongVault.Application.Common.Exceptions;
using SongVault.Application.Files.Stems;
using SongVault.Application.Tests.Fakes;
using SongVault.Domain.Common;
using SongVault.Domain.Files;
using SongVault.Domain.Songs;

namespace SongVault.Application.Tests.Files;

public sealed class StemHandlersTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 16, 0, 0, TimeSpan.Zero);

    private readonly FakeSongRepository _songs = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FixedTimeProvider _clock = new(Now.AddHours(1));

    private (Song Song, SongVersion Version, SongFile Voice, SongFile Drums) ExistingVersion()
    {
        var song = Song.Create(Guid.CreateVersion7(), "Nocturne", null, null, Now);
        var version = song.AddVersion("Maquette", SongVersionStatus.Demo, null, null, Now);
        var voice = version.AddFile("voix.wav", "k1", "audio/wav", 10, SongFileType.Audio, Now);
        var drums = version.AddFile("batterie.wav", "k2", "audio/wav", 10, SongFileType.Audio, Now);
        _songs.Songs.Add(song);
        return (song, version, voice, drums);
    }

    private SetStemsHandler SetHandler() => new(_songs, _uow, _clock);

    [Fact]
    public async Task Set_enregistre_les_pistes_et_les_rend_dans_l_ordre()
    {
        var (song, version, voice, drums) = ExistingVersion();

        var stems = await SetHandler().HandleAsync(
            new SetStemsCommand(song.Id, version.Id, [new(drums.Id, "Batterie"), new(voice.Id, "Voix")]), default);

        Assert.Equal(["Batterie", "Voix"], stems.Select(s => s.Name));
        Assert.Equal([0, 1], stems.Select(s => s.Position));
        Assert.Equal(1, _uow.SaveCount);
        Assert.Equal(Now.AddHours(1), version.UpdatedAt);
    }

    [Fact]
    public async Task Set_laisse_passer_l_erreur_du_domaine_sans_enregistrer()
    {
        var (song, version, voice, _) = ExistingVersion();

        await Assert.ThrowsAsync<DomainException>(() => SetHandler().HandleAsync(
            new SetStemsCommand(song.Id, version.Id, [new(voice.Id, " ")]), default));

        Assert.Equal(0, _uow.SaveCount);
    }

    [Fact]
    public async Task Set_rend_404_pour_une_version_inconnue()
    {
        var (song, _, voice, _) = ExistingVersion();

        await Assert.ThrowsAsync<NotFoundException>(() => SetHandler().HandleAsync(
            new SetStemsCommand(song.Id, Guid.CreateVersion7(), [new(voice.Id, "Voix")]), default));
    }

    [Fact]
    public async Task Get_rend_une_liste_vide_pour_une_version_sans_pistes()
    {
        var (song, version, _, _) = ExistingVersion();

        var stems = await new GetStemsHandler(_songs).HandleAsync(song.Id, version.Id, default);

        Assert.Empty(stems);
    }

    [Fact]
    public async Task Get_rend_404_pour_une_version_a_la_corbeille()
    {
        var (song, version, _, _) = ExistingVersion();
        song.TrashVersion(version.Id, "ana", Now);

        await Assert.ThrowsAsync<NotFoundException>(() => new GetStemsHandler(_songs).HandleAsync(song.Id, version.Id, default));
    }

    [Fact]
    public async Task Une_piste_n_est_jouable_qu_une_fois_saine_et_son_apercu_pret()
    {
        var (song, version, voice, drums) = ExistingVersion();
        version.SetStems([new(voice.Id, "Voix"), new(drums.Id, "Batterie")], Now);
        _songs.Scans[voice.Id] = FileScanStatus.Clean;
        _songs.PreviewDurations[voice.Id] = 180_000;
        _songs.Scans[drums.Id] = FileScanStatus.Pending;
        _songs.PreviewDurations[drums.Id] = 180_000;

        var stems = await new GetStemsHandler(_songs).HandleAsync(song.Id, version.Id, default);

        Assert.Equal([true, false], stems.Select(s => s.Playable));
        Assert.Equal(180_000, stems[0].DurationMs);
    }

    [Fact]
    public void Sans_apercu_pret_une_piste_saine_n_est_pas_jouable()
    {
        var stem = StemDto.Create(Guid.CreateVersion7(), "Voix", 0, "voix.wav", FileScanStatus.NotScanned, null);

        Assert.False(stem.Playable);
    }
}