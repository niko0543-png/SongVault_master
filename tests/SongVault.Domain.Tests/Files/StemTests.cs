using SongVault.Domain.Common;
using SongVault.Domain.Files;
using SongVault.Domain.Songs;

namespace SongVault.Domain.Tests.Files;

/// <summary>Pistes du mélangeur (#23) : des fichiers audio de la version, nommés, dans l'ordre choisi.</summary>
public sealed class StemTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 16, 0, 0, TimeSpan.Zero);

    private static (Song Song, SongVersion Version, SongFile Voice, SongFile Drums, SongFile Pdf) VersionWithFiles()
    {
        var song = Song.Create(Guid.NewGuid(), "Nocturne", null, null, Now.AddDays(-1));
        var version = song.AddVersion("Maquette", SongVersionStatus.Demo, null, null, Now.AddDays(-1));
        var voice = version.AddFile("voix.wav", "k1", "audio/wav", 10, SongFileType.Audio, Now.AddDays(-1));
        var drums = version.AddFile("batterie.wav", "k2", "audio/wav", 10, SongFileType.Audio, Now.AddDays(-1));
        var pdf = version.AddFile("paroles.pdf", "k3", "application/pdf", 5, SongFileType.Document, Now.AddDays(-1));
        return (song, version, voice, drums, pdf);
    }

    [Fact]
    public void SetStems_nomme_et_range_les_pistes_dans_l_ordre_de_la_liste()
    {
        var (_, version, voice, drums, _) = VersionWithFiles();

        version.SetStems([new(drums.Id, " Batterie "), new(voice.Id, "Voix")], Now);

        Assert.Equal(("Batterie", 0), (drums.StemName, drums.StemPosition));
        Assert.Equal(("Voix", 1), (voice.StemName, voice.StemPosition));
        Assert.Equal(Now, version.UpdatedAt);
    }

    [Fact]
    public void SetStems_retire_les_pistes_absentes_de_la_nouvelle_liste()
    {
        var (_, version, voice, drums, _) = VersionWithFiles();
        version.SetStems([new(voice.Id, "Voix"), new(drums.Id, "Batterie")], Now);

        version.SetStems([new(drums.Id, "Batterie")], Now);

        Assert.False(voice.IsStem);
        Assert.Null(voice.StemName);
        Assert.Equal(0, drums.StemPosition);
    }

    [Fact]
    public void SetStems_accepte_une_liste_vide()
    {
        var (_, version, voice, _, _) = VersionWithFiles();
        version.SetStems([new(voice.Id, "Voix")], Now);

        version.SetStems([], Now);

        Assert.DoesNotContain(version.Files, f => f.IsStem);
    }

    [Fact]
    public void SetStems_refuse_un_fichier_qui_n_est_pas_audio_sans_rien_changer()
    {
        var (_, version, voice, _, pdf) = VersionWithFiles();
        version.SetStems([new(voice.Id, "Voix")], Now);

        var ex = Assert.Throws<DomainException>(() => version.SetStems([new(pdf.Id, "Paroles")], Now));

        Assert.Contains("paroles.pdf", ex.Message);
        Assert.Equal("Voix", voice.StemName);
    }

    [Fact]
    public void SetStems_refuse_un_fichier_d_une_autre_version()
    {
        var (song, version, _, _, _) = VersionWithFiles();
        var other = song.AddVersion("Live", SongVersionStatus.Demo, null, null, Now);
        var live = other.AddFile("live.mp3", "k9", "audio/mpeg", 10, SongFileType.Audio, Now);

        Assert.Throws<DomainException>(() => version.SetStems([new(live.Id, "Live")], Now));
    }

    [Fact]
    public void SetStems_refuse_un_fichier_a_la_corbeille()
    {
        var (_, version, voice, _, _) = VersionWithFiles();
        version.TrashFile(voice.Id, "ana", Now);

        Assert.Throws<DomainException>(() => version.SetStems([new(voice.Id, "Voix")], Now));
    }

    [Fact]
    public void SetStems_refuse_deux_fois_le_meme_fichier()
    {
        var (_, version, voice, _, _) = VersionWithFiles();

        Assert.Throws<DomainException>(() => version.SetStems([new(voice.Id, "Voix"), new(voice.Id, "Chœurs")], Now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SetStems_refuse_une_piste_sans_nom(string name)
    {
        var (_, version, voice, _, _) = VersionWithFiles();

        Assert.Throws<DomainException>(() => version.SetStems([new(voice.Id, name)], Now));
    }

    [Fact]
    public void SetStems_refuse_un_nom_de_plus_de_60_caracteres()
    {
        var (_, version, voice, _, _) = VersionWithFiles();

        Assert.Throws<DomainException>(() => version.SetStems([new(voice.Id, new string('a', 61))], Now));
    }

    [Fact]
    public void SetStems_refuse_plus_de_8_pistes()
    {
        var (_, version, _, _, _) = VersionWithFiles();
        var files = Enumerable.Range(1, 9)
            .Select(i => version.AddFile($"piste{i}.wav", $"p{i}", "audio/wav", 10, SongFileType.Audio, Now))
            .ToList();

        var ex = Assert.Throws<DomainException>(() => version.SetStems([.. files.Select(f => new StemChoice(f.Id, f.OriginalFileName))], Now));

        Assert.Contains("8 pistes", ex.Message);
        Assert.DoesNotContain(version.Files, f => f.IsStem);
    }

    [Fact]
    public void TrashFile_retire_le_fichier_du_melangeur()
    {
        var (_, version, voice, drums, _) = VersionWithFiles();
        version.SetStems([new(voice.Id, "Voix"), new(drums.Id, "Batterie")], Now);

        version.TrashFile(voice.Id, "ana", Now);
        version.RestoreFile(voice.Id);

        Assert.False(voice.IsStem);
        Assert.True(drums.IsStem);
    }

    [Fact]
    public void La_corbeille_de_la_version_garde_les_pistes()
    {
        var (song, version, voice, _, _) = VersionWithFiles();
        version.SetStems([new(voice.Id, "Voix")], Now);

        song.TrashVersion(version.Id, "ana", Now);
        song.RestoreVersion(version.Id);

        Assert.Equal(("Voix", 0), (voice.StemName, voice.StemPosition));
    }
}