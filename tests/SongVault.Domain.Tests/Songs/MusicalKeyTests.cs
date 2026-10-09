using SongVault.Domain.Common;
using SongVault.Domain.Songs;

namespace SongVault.Domain.Tests.Songs;

public sealed class MusicalKeyTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("C")]
    [InlineData("F#m")]
    [InlineData("Bb")]
    [InlineData("Ebm")]
    [InlineData(" A ")]
    public void Parse_accepte_les_tonalites_valides(string value)
        => Assert.Equal(value.Trim(), MusicalKey.Parse(value).Value);

    [Theory]
    [InlineData("H")]      // notation allemande : non supportée
    [InlineData("c")]      // minuscule
    [InlineData("F##")]
    [InlineData("Cmaj")]
    [InlineData("")]
    public void Parse_refuse_les_tonalites_invalides(string value)
        => Assert.Throws<DomainException>(() => MusicalKey.Parse(value));

    [Fact]
    public void ParseOptional_renvoie_null_pour_une_chaine_vide()
        => Assert.Null(MusicalKey.ParseOptional("  "));

    [Fact]
    public void Deux_tonalites_identiques_sont_egales()
        => Assert.Equal(MusicalKey.Parse("F#m"), MusicalKey.Parse("F#m"));

    [Theory]
    [InlineData(19)]
    [InlineData(301)]
    public void AddVersion_refuse_un_bpm_hors_limites(int bpm)
    {
        var song = Song.Create(Guid.NewGuid(), "Nocturne", null, null, Now);
        Assert.Throws<DomainException>(() => song.AddVersion("v1", SongVersionStatus.Demo, null, null, Now, bpm));
        Assert.Equal(0, song.LastVersionNumber);
    }

    [Fact]
    public void AddVersion_enregistre_bpm_et_tonalite()
    {
        var song = Song.Create(Guid.NewGuid(), "Nocturne", null, null, Now);
        var version = song.AddVersion("v1", SongVersionStatus.Demo, null, null, Now, 92, MusicalKey.Parse("F#m"));

        Assert.Equal(92, version.Bpm);
        Assert.Equal("F#m", version.Key?.Value);
    }
}