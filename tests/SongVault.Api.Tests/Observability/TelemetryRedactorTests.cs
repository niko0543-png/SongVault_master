using Microsoft.AspNetCore.Http;

using SongVault.Api.Observability;

namespace SongVault.Api.Tests.Observability;

/// <summary>#18 : ce qui ne doit jamais partir dans la télémétrie (ADR 0025).</summary>
[Trait("Category", "Security")]
public sealed class TelemetryRedactorTests
{
    [Fact]
    public void Retire_les_adresses_e_mail()
        => Assert.Equal("E-mail pour [e-mail] et [e-mail].",
            TelemetryRedactor.Redact("E-mail pour niko@example.com et Jean.Dupont+test@mail.songvault.eu."));

    [Theory]
    [InlineData("/api/share/AbC-123_x/files/42/content", "/api/share/{token}/files/42/content")]
    [InlineData("/api/invitations/AbC123/accept", "/api/invitations/{token}/accept")]
    [InlineData("https://songvault.eu/s/AbC123", "https://songvault.eu/s/{token}")]
    [InlineData("/invite/AbC123", "/invite/{token}")]
    public void Retire_les_jetons_des_chemins(string path, string expected)
        => Assert.Equal(expected, TelemetryRedactor.Redact(path));

    [Fact]
    public void Retire_les_valeurs_de_la_chaine_de_requete()
        => Assert.Equal("/reset-password?email=Redacted&code=Redacted",
            TelemetryRedactor.Redact("/reset-password?email=niko%40example.com&code=CfDJ8abc"));

    [Fact]
    public void Un_texte_sans_rien_a_retirer_reste_le_meme()
    {
        const string text = "Tâche file.scan 3f2b en échec (essai 3/3).";
        Assert.Same(text, TelemetryRedactor.Redact(text));
        Assert.Same(text, TelemetryRedactor.Redact(TelemetryRedactor.Redact(text)));
        Assert.Equal("/api/share/{token}", TelemetryRedactor.Redact("/api/share/{token}"));
    }

    [Fact]
    public void Les_objets_sont_nettoyes_les_nombres_gardes()
    {
        Assert.Equal("/api/share/{token}", TelemetryRedactor.RedactValue(new PathString("/api/share/secret")));
        object path = new PathString("/api/bands");
        Assert.Same(path, TelemetryRedactor.RedactValue(path));     // rien à retirer : l'objet lui-même
        Assert.Equal(42, TelemetryRedactor.RedactValue(42));
        Assert.Null(TelemetryRedactor.RedactValue(null));
    }
}