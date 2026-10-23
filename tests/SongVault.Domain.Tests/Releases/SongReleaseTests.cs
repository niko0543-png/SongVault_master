using SongVault.Domain.Common;
using SongVault.Domain.Releases;
using SongVault.Domain.Songs;

namespace SongVault.Domain.Tests.Releases;

/// <summary>#38 : infos de sortie (label, code-barres, liens d'écoute) et sortie d'un morceau.</summary>
public sealed class SongReleaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid BandId = Guid.CreateVersion7();

    [Theory]
    [InlineData("https://open.spotify.com/track/4uLU6hMCjMI75M1A2tKUQC", StreamingPlatform.Spotify)]
    [InlineData("https://music.apple.com/fr/album/nocturne/123", StreamingPlatform.AppleMusic)]
    [InlineData("https://www.deezer.com/fr/track/3135556", StreamingPlatform.Deezer)]
    [InlineData("https://music.youtube.com/watch?v=abc", StreamingPlatform.YouTubeMusic)]
    [InlineData("https://youtu.be/abc", StreamingPlatform.YouTube)]
    [InlineData("https://www.youtube.com/watch?v=abc", StreamingPlatform.YouTube)]
    [InlineData("https://lesnuits.bandcamp.com/track/nocturne", StreamingPlatform.Bandcamp)]
    [InlineData("https://soundcloud.com/lesnuits/nocturne", StreamingPlatform.SoundCloud)]
    [InlineData("https://tidal.com/browse/track/1", StreamingPlatform.Tidal)]
    [InlineData("https://music.amazon.fr/albums/B0", StreamingPlatform.AmazonMusic)]
    [InlineData("https://lesnuits.fr/ecouter", StreamingPlatform.Other)]
    [InlineData("https://notspotify.com/x", StreamingPlatform.Other)]
    public void Un_lien_d_ecoute_reconnait_sa_plateforme(string url, StreamingPlatform platform)
        => Assert.Equal(new StreamingLink(platform, url), StreamingLink.Parse(url));

    [Theory]
    [InlineData("open.spotify.com/track/1")]
    [InlineData("ftp://open.spotify.com/track/1")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://localhost/x")]
    public void Un_lien_qui_n_est_pas_une_adresse_web_est_refuse(string url)
        => Assert.Throws<DomainException>(() => StreamingLink.Parse(url));

    [Theory]
    [InlineData("036000291452", "036000291452")]
    [InlineData("4006381333931", "4006381333931")]
    [InlineData("4 006381 333931", "4006381333931")]
    public void Un_code_barres_valide_est_garde_sans_espaces(string input, string expected)
        => Assert.Equal(expected, Upc.Parse(input).Value);

    [Theory]
    [InlineData("036000291453")]     // mauvaise clé
    [InlineData("12345")]
    [InlineData("ABCDEFGHIJKL")]
    public void Un_code_barres_invalide_est_refuse(string input)
        => Assert.Throws<DomainException>(() => Upc.Parse(input));

    [Fact]
    public void SetDistribution_remplace_label_code_et_liens_en_ignorant_les_lignes_vides()
    {
        var release = Release.Create(BandId, "Nocturne", ReleaseType.Single, null, Now);
        release.SetDistribution("  Nuit Records ", Upc.Parse("036000291452"),
            ["https://open.spotify.com/track/1", " ", "https://www.deezer.com/track/2"], Now.AddHours(1));

        Assert.Equal("Nuit Records", release.Label);
        Assert.Equal("036000291452", release.Upc!.Value);
        Assert.Equal([StreamingPlatform.Spotify, StreamingPlatform.Deezer], release.StreamingLinks.Select(l => l.Platform));
        Assert.Equal(Now.AddHours(1), release.UpdatedAt);
    }

    [Fact]
    public void SetDistribution_refuse_un_lien_en_double_sans_rien_changer()
    {
        var release = Release.Create(BandId, "Nocturne", ReleaseType.Single, null, Now);
        release.SetDistribution("Label", null, ["https://open.spotify.com/track/1"], Now);

        Assert.Throws<DomainException>(() => release.SetDistribution(null, null,
            ["https://open.spotify.com/track/1", "HTTPS://open.spotify.com/track/1"], Now));
        Assert.Equal("Label", release.Label);
        Assert.Single(release.StreamingLinks);
    }

    [Fact]
    public void SetDistribution_refuse_plus_de_douze_liens()
        => Assert.Throws<DomainException>(() => Release.Create(BandId, "N", ReleaseType.Single, null, Now)
            .SetDistribution(null, null, [.. Enumerable.Range(1, Release.MaxStreamingLinks + 1).Select(i => $"https://site{i}.fr/")], Now));

    [Fact]
    public void PutTrack_ajoute_le_morceau_en_dernier_ou_change_sa_version()
    {
        var release = Release.Create(BandId, "Nuits blanches", ReleaseType.EP, null, Now);
        var a = Guid.CreateVersion7();
        var b = Guid.CreateVersion7();
        release.SetTracks([new(a, Guid.CreateVersion7())], Now);

        var newVersion = Guid.CreateVersion7();
        release.PutTrack(b, Guid.CreateVersion7(), Now);
        release.PutTrack(a, newVersion, Now);

        Assert.Equal([(1, a), (2, b)], release.Tracks.Select(t => (t.TrackNumber, t.SongId)));
        Assert.Equal(newVersion, release.Tracks[0].VersionId);
    }

    private static (Song Song, SongVersion V1, SongVersion V2) SongWithTwoVersions(SongVersionStatus lastStatus = SongVersionStatus.Final)
    {
        var song = Song.Create(BandId, "Nocturne", null, null, Now);
        var v1 = song.AddVersion("Maquette", SongVersionStatus.Demo, null, null, Now);
        var v2 = song.AddVersion("Mix final", lastStatus, null, null, Now);
        return (song, v1, v2);
    }

    [Fact]
    public void Seule_la_derniere_version_au_statut_Final_peut_sortir()
    {
        var (song, v1, v2) = SongWithTwoVersions();

        Assert.Same(v2, song.EnsureReleasable(v2.Id));
        var ex = Assert.Throws<DomainException>(() => song.EnsureReleasable(v1.Id));
        Assert.Contains("v2", ex.Message);
    }

    [Fact]
    public void Une_derniere_version_qui_n_est_pas_Final_ne_sort_pas()
    {
        var (song, _, v2) = SongWithTwoVersions(SongVersionStatus.Studio);
        Assert.Throws<DomainException>(() => song.EnsureReleasable(v2.Id));
    }

    [Fact]
    public void Une_version_plus_recente_a_la_corbeille_ne_compte_pas()
    {
        var (song, v1, v2) = SongWithTwoVersions(SongVersionStatus.Demo);
        v1.Update("Maquette", SongVersionStatus.Final, null, null, Now);
        song.TrashVersion(v2.Id, "ana", Now);

        Assert.Same(v1, song.EnsureReleasable(v1.Id));
    }

    [Fact]
    public void Un_morceau_sorti_garde_sa_sortie_jusqu_a_ce_qu_on_le_remette_en_travail()
    {
        var (song, _, _) = SongWithTwoVersions();
        var releaseId = Guid.CreateVersion7();

        song.MarkReleased(releaseId, Now.AddDays(1));
        Assert.True(song.IsReleased);
        Assert.Equal((releaseId, Now.AddDays(1)), (song.ReleaseId!.Value, song.ReleasedAt!.Value));
        Assert.Throws<DomainException>(() => song.MarkReleased(releaseId, Now));

        Assert.True(song.Unrelease(Now.AddDays(2)));
        Assert.False(song.IsReleased);
        Assert.Null(song.ReleasedAt);
        Assert.False(song.Unrelease(Now.AddDays(3)));
    }
}
