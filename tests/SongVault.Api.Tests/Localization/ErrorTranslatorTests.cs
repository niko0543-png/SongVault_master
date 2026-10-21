using SongVault.Api.Localization;

namespace SongVault.Api.Tests.Localization;

public sealed class ErrorTranslatorTests
{
    [Fact]
    public void Un_message_du_catalogue_passe_en_anglais_avec_son_code()
    {
        var (code, text) = ErrorTranslator.Translate("Le fichier est vide.", Language.English);

        Assert.Equal("files.empty", code);
        Assert.Equal("The file is empty.", text);
    }

    [Fact]
    public void En_francais_le_texte_reste_celui_du_code_et_le_code_est_le_meme()
    {
        var (code, text) = ErrorTranslator.Translate("Le fichier est vide.", Language.French);

        Assert.Equal("files.empty", code);
        Assert.Equal("Le fichier est vide.", text);
    }

    [Fact]
    public void Les_parties_variables_sont_reprises_telles_quelles()
    {
        var (code, text) = ErrorTranslator.Translate(
            "Ce morceau figure sur « Été 2026 », « Nocturne ». Retirez-le de ces sorties avant de le supprimer.", Language.English);

        Assert.Equal("songs.onReleases", code);
        Assert.Equal("This song is on « Été 2026 », « Nocturne ». Remove it from these releases before deleting it.", text);
    }

    [Fact]
    public void Une_liste_d_elements_du_catalogue_est_traduite_element_par_element()
        => Assert.Equal("No audio file in the chosen version for: track 1 “Nocturne”, track 3 “Été”.",
            ErrorTranslator.Translate("Aucun fichier audio dans la version choisie pour : piste 1 « Nocturne », piste 3 « Été ».",
                Language.English).Text);

    [Fact]
    public void Un_nom_de_champ_est_traduit()
        => Assert.Equal("The band name field cannot exceed 100 characters.",
            ErrorTranslator.Translate("Le champ nom du groupe ne peut pas dépasser 100 caractères.", Language.English).Text);

    [Fact]
    public void Introuvable_garde_la_ressource_et_la_cle()
        => Assert.Equal("Song '3f2c' not found.", ErrorTranslator.Translate("Song '3f2c' introuvable.", Language.English).Text);

    [Fact]
    public void Un_message_inconnu_reste_en_francais_sans_code()
    {
        var (code, text) = ErrorTranslator.Translate("Message ajouté sans traduction.", Language.English);

        Assert.Null(code);
        Assert.Equal("Message ajouté sans traduction.", text);
    }

    [Fact]
    public void Le_catalogue_a_des_codes_uniques_et_les_memes_parties_variables_en_anglais()
    {
        Assert.Equal(ErrorCatalog.Messages.Count, ErrorCatalog.Messages.Select(m => m.Code).Distinct().Count());
        Assert.Equal(ErrorCatalog.Messages.Count, ErrorCatalog.Messages.Select(m => m.French).Distinct().Count());
        static string Holes(string s) => string.Join(",", System.Text.RegularExpressions.Regex.Matches(s, @"\{\d+\}").Select(m => m.Value).Order());
        Assert.All(ErrorCatalog.Messages, m => Assert.Equal(Holes(m.French), Holes(m.English)));
    }

    [Theory]
    [InlineData("en-US,en;q=0.9,fr;q=0.8", Language.English)]
    [InlineData("fr-FR,fr;q=0.9,en;q=0.8", Language.French)]
    [InlineData("de-DE,en;q=0.5", Language.English)]
    [InlineData("de-DE", Language.French)]
    [InlineData("fr;q=0.4,en;q=0.6", Language.English)]
    [InlineData("", Language.French)]
    public void La_langue_vient_de_Accept_Language(string header, Language expected)
    {
        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        if (header != "") context.Request.Headers.AcceptLanguage = header;

        Assert.Equal(expected, RequestLanguage.From(context.Request));
    }
}
