using SongVault.Domain.Common;
using SongVault.Domain.Setlists;

namespace SongVault.Domain.Tests.Setlists;

public sealed class SetlistTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid BandId = Guid.CreateVersion7();

    private static Setlist New() => Setlist.Create(BandId, "  Concert au Ferrailleur  ", new DateOnly(2026, 11, 28), "  Nantes  ", Now);

    private static SetlistItemChoice Item(int? durationMs = null) => new(Guid.CreateVersion7(), DurationMs: durationMs);

    private static IEnumerable<Guid> SongsInOrder(Setlist setlist) => setlist.Items.Select(i => i.SongId);

    [Fact]
    public void Create_nettoie_nom_et_lieu_et_garde_la_date()
    {
        var setlist = New();

        Assert.Equal("Concert au Ferrailleur", setlist.Name);
        Assert.Equal("Nantes", setlist.Venue);
        Assert.Equal(new DateOnly(2026, 11, 28), setlist.Date);
        Assert.Empty(setlist.Items);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_refuse_un_nom_vide(string name)
        => Assert.Throws<DomainException>(() => Setlist.Create(BandId, name, null, null, Now));

    [Fact]
    public void Un_lieu_vide_devient_null()
        => Assert.Null(Setlist.Create(BandId, "Répétition", null, "   ", Now).Venue);

    [Fact]
    public void SetItems_place_les_morceaux_dans_l_ordre_de_la_liste()
    {
        var setlist = New();
        var a = Item();
        var b = Item();
        var c = Item();

        setlist.SetItems([b, a, c], Now);

        Assert.Equal([b.SongId, a.SongId, c.SongId], SongsInOrder(setlist));
        Assert.Equal([1, 2, 3], setlist.Items.Select(i => i.Position));
    }

    [Fact]
    public void SetItems_reordonne_et_garde_notes_version_et_duree()
    {
        var setlist = New();
        var a = Item();
        var b = Item();
        var c = Item();
        setlist.SetItems([a, b, c], Now);
        var version = Guid.CreateVersion7();

        setlist.SetItems([c, a with { VersionId = version, Notes = "  capo 2  ", DurationMs = 215_000 }, b], Now.AddMinutes(1));

        Assert.Equal([c.SongId, a.SongId, b.SongId], SongsInOrder(setlist));
        Assert.Equal([1, 2, 3], setlist.Items.Select(i => i.Position));
        var moved = setlist.Items[1];
        Assert.Equal((version, "capo 2", 215_000), (moved.VersionId, moved.Notes, moved.DurationMs));
        Assert.Equal(Now.AddMinutes(1), setlist.UpdatedAt);
    }

    [Fact]
    public void SetItems_retire_les_morceaux_absents_et_renumerote()
    {
        var setlist = New();
        var a = Item();
        var b = Item();
        var c = Item();
        setlist.SetItems([a, b, c], Now);

        setlist.SetItems([a, c], Now);

        Assert.Equal([a.SongId, c.SongId], SongsInOrder(setlist));
        Assert.Equal([1, 2], setlist.Items.Select(i => i.Position));
    }

    [Fact]
    public void ParkPositions_passe_en_negatif_sans_changer_l_ordre()
    {
        var setlist = New();
        var a = Item();
        var b = Item();
        setlist.SetItems([a, b], Now);

        setlist.ParkPositions();

        Assert.Equal([a.SongId, b.SongId], SongsInOrder(setlist));
        Assert.Equal([-2, -1], setlist.Items.Select(i => i.Position));
    }

    [Fact]
    public void SetItems_refuse_un_morceau_en_double()
    {
        var a = Item();
        Assert.Throws<DomainException>(() => New().SetItems([a, a], Now));
    }

    [Theory]
    [InlineData(999)]
    [InlineData(3_600_001)]
    public void SetItems_refuse_une_duree_hors_limites(int durationMs)
        => Assert.Throws<DomainException>(() => New().SetItems([Item(durationMs)], Now));

    [Fact]
    public void SetItems_refuse_plus_de_50_morceaux()
        => Assert.Throws<DomainException>(
            () => New().SetItems([.. Enumerable.Range(0, Setlist.MaxItems + 1).Select(_ => Item())], Now));

    [Fact]
    public void SetItems_refuse_des_notes_trop_longues()
        => Assert.Throws<DomainException>(
            () => New().SetItems([Item() with { Notes = new string('x', SetlistItem.NotesMaxLength + 1) }], Now));
}