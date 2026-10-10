using System.Security.Cryptography;
using System.Text;

using SongVault.Application.Invitations;
using SongVault.Domain.Bands;

namespace SongVault.Application.Tests.Invitations;

public sealed class InvitationTokenTests
{
    [Fact]
    public void New_donne_43_caracteres_Base64Url_et_leur_SHA256()
    {
        var (token, hash) = InvitationToken.New();

        Assert.Equal(43, token.Length);                                    // 32 octets, sans remplissage
        Assert.Matches("^[A-Za-z0-9_-]+$", token);                         // utilisable tel quel dans une URL
        Assert.Equal(SHA256.HashData(Encoding.UTF8.GetBytes(token)), hash);
    }

    [Fact]
    public void Deux_jetons_sont_differents()
        => Assert.NotEqual(InvitationToken.New().Token, InvitationToken.New().Token);

    [Fact]
    public void Email_encode_le_nom_du_groupe_dans_le_HTML()
    {
        var message = InvitationEmail.Build("a@b.c", "<script>", "owner@b.c", BandRole.Member,
            "http://localhost:8080/invite/abc", new DateTimeOffset(2026, 10, 16, 0, 0, 0, TimeSpan.Zero));

        Assert.Contains("&lt;script&gt;", message.HtmlBody);
        Assert.DoesNotContain("<script>", message.HtmlBody);
        Assert.Contains("http://localhost:8080/invite/abc", message.TextBody);
        Assert.Contains("16 octobre 2026", message.TextBody);
    }
}