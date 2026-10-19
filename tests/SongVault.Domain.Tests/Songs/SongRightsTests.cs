using SongVault.Domain.Common;
using SongVault.Domain.Songs;

namespace SongVault.Domain.Tests.Songs;

public sealed class SongRightsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);

    private static Song NewSong() => Song.Create(Guid.NewGuid(), "Nocturne", null, null, Now.AddDays(-1));

    private static CreditChoice C(string name, CreditRole role, decimal share) => new(name, role, share);

    [Fact]
    public void SetRights_refuse_un_total_a_99_pourcent()
    {
        var song = NewSong();

        var ex = Assert.Throws<DomainException>(() => song.SetRights(null,
            [C("Alice", CreditRole.Author, 50m), C("Bruno", CreditRole.Composer, 49m)], Now));

        Assert.Contains("100 %", ex.Message);
        Assert.Contains("99 %", ex.Message);
        Assert.Empty(song.Credits);
    }

    [Fact]
    public void SetRights_refuse_un_total_au_dessus_de_100_pourcent()
    {
        var song = NewSong();
        Assert.Throws<DomainException>(() => song.SetRights(null,
            [C("Alice", CreditRole.Author, 60m), C("Bruno", CreditRole.Composer, 40.01m)], Now));
    }

    [Fact]
    public void SetRights_accepte_un_total_de_100_pourcent_avec_des_decimales()
    {
        var song = NewSong();

        song.SetRights(null,
            [C(" Alice ", CreditRole.Author, 33.33m), C("Bruno", CreditRole.Composer, 33.33m), C("Chloé", CreditRole.Arranger, 33.34m)], Now);

        Assert.Equal(["Alice", "Bruno", "Chloé"], song.Credits.Select(c => c.Name));
        Assert.Equal([1, 2, 3], song.Credits.Select(c => c.Position));
        Assert.Equal(100m, song.Credits.Sum(c => c.SharePercent));
        Assert.Equal(Now, song.UpdatedAt);
    }

    [Fact]
    public void SetRights_accepte_une_liste_vide_et_remplace_les_anciens_credits()
    {
        var song = NewSong();
        song.SetRights(null, [C("Alice", CreditRole.Author, 100m)], Now);

        song.SetRights(null, [], Now);

        Assert.Empty(song.Credits);
    }

    [Fact]
    public void SetRights_un_refus_laisse_les_credits_precedents_en_place()
    {
        var song = NewSong();
        song.SetRights(Iswc.Parse("T-034.524.680-1"), [C("Alice", CreditRole.Author, 100m)], Now);

        Assert.Throws<DomainException>(() => song.SetRights(null, [C("Bruno", CreditRole.Author, 90m)], Now.AddHours(1)));

        Assert.Equal("Alice", Assert.Single(song.Credits).Name);
        Assert.NotNull(song.Iswc);
        Assert.Equal(Now, song.UpdatedAt);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-10")]
    [InlineData("100.01")]
    [InlineData("33.333")]
    public void SetRights_refuse_une_part_invalide(string share)
    {
        var song = NewSong();
        var value = decimal.Parse(share, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Throws<DomainException>(() => song.SetRights(null, [C("Alice", CreditRole.Author, value)], Now));
    }

    [Fact]
    public void SetRights_refuse_un_nom_vide_ou_trop_long()
    {
        var song = NewSong();
        Assert.Throws<DomainException>(() => song.SetRights(null, [C("  ", CreditRole.Author, 100m)], Now));
        Assert.Throws<DomainException>(() => song.SetRights(null,
            [C(new string('a', Credit.NameMaxLength + 1), CreditRole.Author, 100m)], Now));
    }

    [Fact]
    public void SetRights_refuse_le_meme_nom_deux_fois_avec_le_meme_role()
    {
        var song = NewSong();
        Assert.Throws<DomainException>(() => song.SetRights(null,
            [C("Alice", CreditRole.Author, 50m), C("alice", CreditRole.Author, 50m)], Now));
    }

    [Fact]
    public void SetRights_accepte_le_meme_nom_avec_deux_roles()
    {
        var song = NewSong();
        song.SetRights(null, [C("Alice", CreditRole.Author, 50m), C("Alice", CreditRole.Composer, 50m)], Now);
        Assert.Equal(2, song.Credits.Count);
    }

    [Fact]
    public void SetRights_refuse_un_role_inconnu_et_trop_de_credits()
    {
        var song = NewSong();
        Assert.Throws<DomainException>(() => song.SetRights(null, [C("Alice", (CreditRole)42, 100m)], Now));

        var many = Enumerable.Range(1, Song.MaxCredits + 1).Select(i => C($"Ayant droit {i}", CreditRole.Author, 1m)).ToList();
        Assert.Throws<DomainException>(() => song.SetRights(null, many, Now));
    }

    [Theory]
    [InlineData("T-034.524.680-1", "T-034.524.680-1")]
    [InlineData("T0345246801", "T-034.524.680-1")]
    [InlineData(" t 034 524 680 1 ", "T-034.524.680-1")]
    [InlineData("T-123.456.789-4", "T-123.456.789-4")]
    public void Iswc_accepte_les_saisies_valides_et_les_ecrit_en_forme_standard(string input, string expected)
        => Assert.Equal(expected, Iswc.Parse(input).Value);

    [Theory]
    [InlineData("T-034.524.680-2")] // mauvaise clé
    [InlineData("T-034.524.680")]   // trop court
    [InlineData("X-034.524.680-1")]
    [InlineData("T-034.524.68A-1")]
    public void Iswc_refuse_les_codes_invalides(string input)
        => Assert.Throws<DomainException>(() => Iswc.Parse(input));

    [Fact]
    public void Iswc_ParseOptional_renvoie_null_pour_une_chaine_vide()
        => Assert.Null(Iswc.ParseOptional(" "));

    [Theory]
    [InlineData("FR-Z03-26-00001", "FRZ032600001", "FR-Z03-26-00001")]
    [InlineData("frz032600001", "FRZ032600001", "FR-Z03-26-00001")]
    [InlineData("US RC1 76 07839", "USRC17607839", "US-RC1-76-07839")]
    public void Isrc_accepte_les_saisies_valides(string input, string value, string display)
    {
        var isrc = Isrc.Parse(input);
        Assert.Equal(value, isrc.Value);
        Assert.Equal(display, isrc.Display);
    }

    [Theory]
    [InlineData("F1-Z03-26-00001")] // pays en lettres
    [InlineData("FR-Z03-26-0001")]  // numéro trop court
    [InlineData("FR-Z03-2A-00001")] // année en chiffres
    [InlineData("FR-Z03-26-000012")]
    public void Isrc_refuse_les_codes_invalides(string input)
        => Assert.Throws<DomainException>(() => Isrc.Parse(input));

    [Fact]
    public void SetIsrc_pose_et_retire_le_code_de_la_version()
    {
        var song = NewSong();
        var version = song.AddVersion("Démo", SongVersionStatus.Demo, null, null, Now.AddDays(-1));

        version.SetIsrc(Isrc.Parse("FR-Z03-26-00001"), Now);
        Assert.Equal("FRZ032600001", version.Isrc!.Value);
        Assert.Equal(Now, version.UpdatedAt);

        version.SetIsrc(null, Now);
        Assert.Null(version.Isrc);
    }
}