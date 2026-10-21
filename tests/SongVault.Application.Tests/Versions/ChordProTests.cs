using SongVault.Application.Versions;

namespace SongVault.Application.Tests.Versions;

public sealed class ChordProTests
{
    [Theory]
    [InlineData("Am")]
    [InlineData("F#m7b5")]
    [InlineData("Dsus4")]
    [InlineData("E7(#9)")]
    [InlineData("D/F#")]
    [InlineData("Bbmaj7")]
    public void Un_accord_est_reconnu(string text) => Assert.True(ChordPro.IsChord(text));

    [Theory]
    [InlineData("Refrain")]
    [InlineData("Bridge")]
    [InlineData("x2")]
    [InlineData("H7")]
    [InlineData("")]
    public void Un_titre_de_partie_n_est_pas_un_accord(string text) => Assert.False(ChordPro.IsChord(text));

    [Fact]
    public void Des_paroles_sans_accords_ne_changent_pas()
    {
        const string lyrics = "Couplet 1\n[Refrain]\nLa nuit  tombe ";

        Assert.False(ChordPro.HasChords(lyrics));
        Assert.Equal(lyrics, ChordPro.StripChords(lyrics));
    }

    [Fact]
    public void Les_accords_sont_retires_des_paroles()
    {
        const string source = "{title: Sous la pluie}\n{c: Couplet 1}\n[Am]Je mar[G]che [F] [E7]seul\n[Am] [F] [G]\n\n[Refrain]\nOh [x2]";

        Assert.True(ChordPro.HasChords(source));
        Assert.Equal("Couplet 1\nJe marche seul\n\n[Refrain]\nOh [x2]", ChordPro.StripChords(source));
    }

    [Fact]
    public void Les_fins_de_ligne_Windows_sont_acceptees()
        => Assert.Equal("Un\nDeux", ChordPro.StripChords("[C]Un\r\n[G]Deux"));
}