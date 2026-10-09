using SongVault.Domain.Bands;
using SongVault.Domain.Common;

namespace SongVault.Domain.Tests.Bands;

public sealed class BandTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_avec_donnees_valides_initialise_le_groupe()
    {
        var band = Band.Create("SongVault Band", "owner-1", Now);

        Assert.NotEqual(Guid.Empty, band.Id);
        Assert.Equal("SongVault Band", band.Name);
        Assert.Equal(Now, band.CreatedAt);
    }

    [Fact]
    public void Create_ajoute_le_proprietaire_comme_membre_avec_le_role_Owner()
    {
        var band = Band.Create("SongVault Band", "owner-1", Now);

        var membership = Assert.Single(band.Memberships);
        Assert.Equal(band.Id, membership.BandId);
        Assert.Equal("owner-1", membership.UserId);
        Assert.Equal(BandRole.Owner, membership.Role);
        Assert.Equal(Now, membership.JoinedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_refuse_un_nom_vide(string? name)
    {
        Assert.Throws<DomainException>(() => Band.Create(name!, "owner-1", Now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_refuse_un_proprietaire_vide(string? ownerUserId)
    {
        Assert.Throws<DomainException>(() => Band.Create("SongVault Band", ownerUserId!, Now));
    }

    [Fact]
    public void Create_refuse_un_nom_trop_long()
    {
        var name = new string('a', Band.NameMaxLength + 1);
        Assert.Throws<DomainException>(() => Band.Create(name, "owner-1", Now));
    }

    [Fact]
    public void Create_accepte_un_nom_de_longueur_maximale()
    {
        var band = Band.Create(new string('a', Band.NameMaxLength), "owner-1", Now);
        Assert.Equal(Band.NameMaxLength, band.Name.Length);
    }

    [Fact]
    public void Create_supprime_les_espaces_autour_du_nom()
    {
        var band = Band.Create("  SongVault Band  ", "owner-1", Now);
        Assert.Equal("SongVault Band", band.Name);
    }

    [Fact]
    public void Rename_modifie_le_nom()
    {
        var band = Band.Create("SongVault Band", "owner-1", Now);

        band.Rename("Nouveau Nom");

        Assert.Equal("Nouveau Nom", band.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Rename_refuse_un_nom_vide(string? name)
    {
        var band = Band.Create("SongVault Band", "owner-1", Now);

        Assert.Throws<DomainException>(() => band.Rename(name!));
    }

    [Fact]
    public void Rename_refuse_un_nom_trop_long()
    {
        var band = Band.Create("SongVault Band", "owner-1", Now);
        var name = new string('a', Band.NameMaxLength + 1);

        Assert.Throws<DomainException>(() => band.Rename(name));
    }

    [Fact]
    public void Rename_supprime_les_espaces_autour_du_nom()
    {
        var band = Band.Create("SongVault Band", "owner-1", Now);

        band.Rename("  Nouveau Nom  ");

        Assert.Equal("Nouveau Nom", band.Name);
    }
}
