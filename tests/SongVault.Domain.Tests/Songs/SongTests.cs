using SongVault.Domain.Common;
using SongVault.Domain.Songs;

namespace SongVault.Domain.Tests.Songs;

public sealed class SongTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_avec_donnees_valides_initialise_le_morceau()
    {
        var song = Song.Create("owner-1", "Nocturne", "SongVault Band", "Ballade piano", Now);

        Assert.NotEqual(Guid.Empty, song.Id);
        Assert.Equal("Nocturne", song.Title);
        Assert.Equal(Now, song.CreatedAt);
        Assert.Equal(Now, song.UpdatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_refuse_un_titre_vide(string? title)
    {
        Assert.Throws<DomainException>(() => Song.Create("owner-1", title!, null, null, Now));
    }

    [Fact]
    public void Create_refuse_un_titre_trop_long()
    {
        var title = new string('a', Song.TitleMaxLength + 1);
        Assert.Throws<DomainException>(() => Song.Create("owner-1", title, null, null, Now));
    }

    [Fact]
    public void Create_accepte_un_titre_de_longueur_maximale()
    {
        var song = Song.Create("owner-1", new string('a', Song.TitleMaxLength), null, null, Now);
        Assert.Equal(Song.TitleMaxLength, song.Title.Length);
    }

    [Fact]
    public void Create_supprime_les_espaces_autour_du_titre()
    {
        var song = Song.Create("owner-1", "  Nocturne  ", null, null, Now);
        Assert.Equal("Nocturne", song.Title);
    }

    [Fact]
    public void Create_transforme_un_artiste_blanc_en_null()
    {
        var song = Song.Create("owner-1", "Nocturne", "   ", null, Now);
        Assert.Null(song.Artist);
    }

    [Fact]
    public void UpdateDetails_modifie_les_champs_et_UpdatedAt_mais_pas_CreatedAt()
    {
        var song = Song.Create("owner-1", "Nocturne", null, null, Now);
        var later = Now.AddHours(2);

        song.UpdateDetails("Nocturne (v2)", "Band", "Nouvel arrangement", later);

        Assert.Equal("Nocturne (v2)", song.Title);
        Assert.Equal(Now, song.CreatedAt);
        Assert.Equal(later, song.UpdatedAt);
    }

    [Fact]
    public void UpdateDetails_invalide_ne_modifie_rien()
    {
        var song = Song.Create("owner-1", "Nocturne", null, null, Now);

        Assert.Throws<DomainException>(() => song.UpdateDetails("", null, null, Now.AddHours(1)));

        Assert.Equal("Nocturne", song.Title);
        Assert.Equal(Now, song.UpdatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_refuse_un_morceau_sans_proprietaire(string ownerId)
    => Assert.Throws<DomainException>(() => Song.Create(ownerId, "Nocturne", null, null, Now));
}