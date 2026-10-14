using SongVault.Domain.Comments;

namespace SongVault.Domain.Tests.Comments;

public sealed class CommentMentionsTests
{
    [Theory]
    [InlineData("@ana@exemple.fr peux-tu réécouter ?", "ana@exemple.fr")]
    [InlineData("Merci @Ana@Exemple.FR !", "ana@exemple.fr")]
    [InlineData("Vu avec @ana@exemple.fr.", "ana@exemple.fr")]
    [InlineData("(@ana@exemple.fr) d'accord", "ana@exemple.fr")]
    [InlineData("Ligne 1\n@ana@exemple.fr, ligne 2", "ana@exemple.fr")]
    public void Trouve_l_adresse_apres_l_arobase(string body, string expected)
        => Assert.Equal([expected], CommentMentions.FindEmails(body));

    [Theory]
    [InlineData("Écris à ana@exemple.fr")]                  // une adresse sans @ devant n'est pas une mention
    [InlineData("rdv @ 20 h")]
    [InlineData("@ana sans domaine")]
    [InlineData("")]
    public void Ne_trouve_rien(string body) => Assert.Empty(CommentMentions.FindEmails(body));

    [Fact]
    public void Plusieurs_mentions_sans_doublon()
    {
        var emails = CommentMentions.FindEmails("@ana@exemple.fr et @bo@exemple.fr, puis @ANA@exemple.fr");

        Assert.Equal(new HashSet<string> { "ana@exemple.fr", "bo@exemple.fr" }, emails);
    }
}