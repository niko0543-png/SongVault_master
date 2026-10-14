using SongVault.Domain.Common;
using SongVault.Domain.Releases;

namespace SongVault.Domain.Tests.Releases;

public sealed class ReleaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid BandId = Guid.CreateVersion7();

    private static Release New() => Release.Create(BandId, "  Nuits blanches  ", ReleaseType.EP, new DateOnly(2026, 11, 20), Now);

    private static ReleaseTrackChoice Track() => new(Guid.CreateVersion7(), Guid.CreateVersion7());

    private static IEnumerable<Guid> SongsInOrder(Release release) => release.Tracks.Select(t => t.SongId);

    [Fact]
    public void Create_nettoie_le_titre_et_garde_type_et_date()
    {
        var release = New();

        Assert.Equal("Nuits blanches", release.Title);
        Assert.Equal(ReleaseType.EP, release.Type);
        Assert.Equal(new DateOnly(2026, 11, 20), release.ReleaseDate);
        Assert.Empty(release.Tracks);
        Assert.Null(release.CoverStorageKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_refuse_un_titre_vide(string title)
        => Assert.Throws<DomainException>(() => Release.Create(BandId, title, ReleaseType.Single, null, Now));

    [Fact]
    public void Create_refuse_un_type_inconnu()
        => Assert.Throws<DomainException>(() => Release.Create(BandId, "Démo", (ReleaseType)42, null, Now));

    [Fact]
    public void SetTracks_numerote_dans_l_ordre_de_la_liste()
    {
        var release = New();
        var a = Track();
        var b = Track();
        var c = Track();

        release.SetTracks([b, a, c], Now);

        Assert.Equal([b.SongId, a.SongId, c.SongId], SongsInOrder(release));
        Assert.Equal([1, 2, 3], release.Tracks.Select(t => t.TrackNumber));
    }

    [Fact]
    public void SetTracks_echange_deux_pistes_et_change_la_version()
    {
        var release = New();
        var a = Track();
        var b = Track();
        release.SetTracks([a, b], Now);
        var newVersion = Guid.CreateVersion7();

        release.SetTracks([b, a with { VersionId = newVersion }], Now.AddMinutes(1));

        Assert.Equal([b.SongId, a.SongId], SongsInOrder(release));
        Assert.Equal(newVersion, release.Tracks[1].VersionId);
        Assert.Equal(Now.AddMinutes(1), release.UpdatedAt);
    }

    [Fact]
    public void SetTracks_retire_les_morceaux_absents_de_la_liste()
    {
        var release = New();
        var a = Track();
        var b = Track();
        release.SetTracks([a, b], Now);

        release.SetTracks([b], Now);

        var only = Assert.Single(release.Tracks);
        Assert.Equal(b.SongId, only.SongId);
        Assert.Equal(1, only.TrackNumber);
    }

    [Fact]
    public void SetTracks_refuse_deux_fois_le_meme_morceau()
    {
        var release = New();
        var a = Track();

        Assert.Throws<DomainException>(() => release.SetTracks([a, a with { VersionId = Guid.CreateVersion7() }], Now));
        Assert.Empty(release.Tracks);
    }

    [Fact]
    public void SetTracks_refuse_trop_de_pistes()
    {
        var release = New();
        var tracks = Enumerable.Range(0, Release.MaxTracks + 1).Select(_ => Track()).ToList();

        Assert.Throws<DomainException>(() => release.SetTracks(tracks, Now));
    }

    [Fact]
    public void ParkTrackNumbers_passe_les_numeros_en_negatif_sans_changer_l_ordre()
    {
        var release = New();
        var a = Track();
        var b = Track();
        release.SetTracks([a, b], Now);

        release.ParkTrackNumbers();

        Assert.Equal([-2, -1], release.Tracks.Select(t => t.TrackNumber));
        Assert.Equal([a.SongId, b.SongId], SongsInOrder(release));
    }

    [Fact]
    public void SetCover_renvoie_l_ancienne_cle()
    {
        var release = New();

        Assert.Null(release.SetCover("a.jpg", "image/jpeg", Now));
        Assert.Equal("a.jpg", release.SetCover("b.png", "image/png", Now));
        Assert.Equal("b.png", release.RemoveCover(Now));
        Assert.Null(release.CoverStorageKey);
        Assert.Null(release.CoverContentType);
    }
}