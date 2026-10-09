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

    private static BandRole RoleOf(Band band, string userId) => band.Memberships.Single(m => m.UserId == userId).Role;

    [Fact]
    public void AddMember_ajoute_un_membre_avec_son_role()
    {
        var band = Band.Create("SongVault Band", "owner-1", Now);

        band.AddMember("guest-1", BandRole.Guest, Now);

        var guest = Assert.Single(band.Memberships, m => m.UserId == "guest-1");
        Assert.Equal(band.Id, guest.BandId);
        Assert.Equal(BandRole.Guest, guest.Role);
        Assert.True(band.HasMember("guest-1"));
    }

    [Fact]
    public void AddMember_refuse_un_utilisateur_deja_membre()
    {
        var band = Band.Create("SongVault Band", "owner-1", Now);

        Assert.Throws<DomainException>(() => band.AddMember("owner-1", BandRole.Member, Now));
    }

    [Fact]
    public void ChangeRole_change_le_role_d_un_membre()
    {
        var band = Band.Create("SongVault Band", "owner-1", Now);
        band.AddMember("member-1", BandRole.Member, Now);

        band.ChangeRole("member-1", BandRole.Guest);

        Assert.Equal(BandRole.Guest, RoleOf(band, "member-1"));
    }

    [Theory]
    [InlineData(BandRole.Member)]
    [InlineData(BandRole.Guest)]
    public void ChangeRole_refuse_de_retrograder_le_dernier_Owner(BandRole role)
    {
        var band = Band.Create("SongVault Band", "owner-1", Now);
        band.AddMember("member-1", BandRole.Member, Now);

        Assert.Throws<LastOwnerException>(() => band.ChangeRole("owner-1", role));
        Assert.Equal(BandRole.Owner, RoleOf(band, "owner-1"));
    }

    [Fact]
    public void ChangeRole_retrograde_un_Owner_quand_il_en_reste_un_autre()
    {
        var band = Band.Create("SongVault Band", "owner-1", Now);
        band.AddMember("owner-2", BandRole.Owner, Now);

        band.ChangeRole("owner-1", BandRole.Member);

        Assert.Equal(BandRole.Member, RoleOf(band, "owner-1"));
    }

    [Fact]
    public void RemoveMember_retire_le_membre()
    {
        var band = Band.Create("SongVault Band", "owner-1", Now);
        band.AddMember("member-1", BandRole.Member, Now);

        band.RemoveMember("member-1");

        Assert.False(band.HasMember("member-1"));
        Assert.Single(band.Memberships);
    }

    [Fact]
    public void RemoveMember_refuse_de_retirer_le_dernier_Owner()
    {
        var band = Band.Create("SongVault Band", "owner-1", Now);
        band.AddMember("member-1", BandRole.Member, Now);

        Assert.Throws<LastOwnerException>(() => band.RemoveMember("owner-1"));
        Assert.True(band.HasMember("owner-1"));
    }

    [Fact]
    public void ChangeRole_et_RemoveMember_refusent_un_non_membre()
    {
        var band = Band.Create("SongVault Band", "owner-1", Now);

        Assert.Throws<DomainException>(() => band.ChangeRole("inconnu", BandRole.Guest));
        Assert.Throws<DomainException>(() => band.RemoveMember("inconnu"));
    }
}
