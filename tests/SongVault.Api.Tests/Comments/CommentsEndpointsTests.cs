using System.Net;
using System.Net.Http.Json;

using SongVault.Api.Contracts.Comments;
using SongVault.Api.Contracts.Files;
using SongVault.Domain.Bands;

namespace SongVault.Api.Tests.Comments;

[Collection(ApiCollection.Name)]
public sealed class CommentsEndpointsTests(SongVaultApiFactory factory)
{
    /// <summary>URL utiles d'un groupe dont l'Owner a déposé un MP3.</summary>
    private sealed record Arranged(HttpClient Owner, Guid BandId, string Song, string Version, string File, string Comments);

    private async Task<Arranged> ArrangeAsync()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var song = (await owner.PostAsJsonAsync($"/api/bands/{bandId}/songs", new { title = "Nocturne" }))
            .Headers.Location!.ToString();
        var version = (await owner.PostAsJsonAsync($"{song}/versions", new { title = "v1", status = "Demo" }))
            .Headers.Location!.ToString();
        var upload = await owner.PostAsync($"{version}/files", TestFiles.Form(TestFiles.Mp3(1024), "maquette.mp3"));
        var file = (await upload.Content.ReadFromJsonAsync<SongFileResponse>(TestJson.Options))!;
        var fileUrl = $"{version}/files/{file.Id}";
        return new Arranged(owner, bandId, song, version, fileUrl, $"{fileUrl}/comments");
    }

    private async Task<HttpClient> MemberAsync(Arranged a, BandRole role)
    {
        var client = await factory.CreateNewUserClientAsync();
        await factory.AddMemberAsync(a.BandId, client, role);
        return client;
    }

    private static async Task<CommentResponse> PostAsync(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CommentResponse>(TestJson.Options))!;
    }

    private static async Task<List<CommentResponse>> ListAsync(HttpClient client, string url)
        => (await client.GetFromJsonAsync<List<CommentResponse>>(url, TestJson.Options))!;

    [Fact]
    public async Task Un_Guest_commente_et_repond_mais_ne_modifie_pas_le_morceau()
    {
        var a = await ArrangeAsync();
        var guest = await MemberAsync(a, BandRole.Guest);

        var comment = await PostAsync(guest, a.Comments, new { body = "À 1:42, la basse décale", positionMs = 102_000 });
        await PostAsync(a.Owner, $"{a.Comments}/{comment.Id}/replies", new { body = "Corrigé dans la v2" });

        Assert.Equal(HttpStatusCode.Forbidden, (await guest.PutAsJsonAsync(a.Song, new { title = "Non" })).StatusCode);
        var thread = Assert.Single(await ListAsync(guest, a.Comments));
        Assert.Equal(102_000, thread.PositionMs);
        Assert.Equal(await guest.UserIdAsync(), thread.AuthorId);
        Assert.NotNull(thread.AuthorEmail);
        var reply = Assert.Single(thread.Replies);
        Assert.Equal("Corrigé dans la v2", reply.Body);
        Assert.Null(reply.PositionMs);
    }

    [Fact]
    public async Task Les_fils_generaux_viennent_d_abord_puis_l_ordre_de_l_ecoute()
    {
        var a = await ArrangeAsync();
        await PostAsync(a.Owner, a.Comments, new { body = "Refrain", positionMs = 60_000 });
        await PostAsync(a.Owner, a.Comments, new { body = "Intro", positionMs = 5_000 });
        await PostAsync(a.Owner, a.Comments, new { body = "Bon mix dans l'ensemble" });

        var bodies = (await ListAsync(a.Owner, a.Comments)).Select(c => c.Body);

        Assert.Equal(new[] { "Bon mix dans l'ensemble", "Intro", "Refrain" }, bodies);
    }

    [Fact]
    public async Task Un_fil_se_resout_et_se_rouvre()
    {
        var a = await ArrangeAsync();
        var guest = await MemberAsync(a, BandRole.Guest);
        var member = await MemberAsync(a, BandRole.Member);
        var comment = await PostAsync(a.Owner, a.Comments, new { body = "Trop de reverb", positionMs = 30_000 });
        var resolve = $"{a.Comments}/{comment.Id}/resolve";

        Assert.Equal(HttpStatusCode.Forbidden, (await guest.PostAsync(resolve, null)).StatusCode);   // pas son fil
        Assert.Equal(HttpStatusCode.NoContent, (await member.PostAsync(resolve, null)).StatusCode);
        var resolved = Assert.Single(await ListAsync(a.Owner, a.Comments));
        Assert.NotNull(resolved.ResolvedAt);
        Assert.NotNull(resolved.ResolvedByEmail);

        Assert.Equal(HttpStatusCode.NoContent, (await a.Owner.DeleteAsync(resolve)).StatusCode);
        Assert.Null(Assert.Single(await ListAsync(a.Owner, a.Comments)).ResolvedAt);
    }

    [Fact]
    public async Task Un_Guest_resout_son_propre_fil()
    {
        var a = await ArrangeAsync();
        var guest = await MemberAsync(a, BandRole.Guest);
        var comment = await PostAsync(guest, a.Comments, new { body = "Voix trop basse" });

        var response = await guest.PostAsync($"{a.Comments}/{comment.Id}/resolve", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Seul_l_auteur_modifie_et_l_Owner_peut_supprimer()
    {
        var a = await ArrangeAsync();
        var member = await MemberAsync(a, BandRole.Member);
        var other = await MemberAsync(a, BandRole.Member);
        var comment = await PostAsync(member, a.Comments, new { body = "Batterie en avance", positionMs = 12_000 });
        var url = $"{a.Comments}/{comment.Id}";

        Assert.Equal(HttpStatusCode.Forbidden, (await other.PutAsJsonAsync(url, new { body = "Non" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.DeleteAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await member.PutAsJsonAsync(url, new { body = "Batterie en avance au 2e couplet" })).StatusCode);
        var edited = Assert.Single(await ListAsync(a.Owner, a.Comments));
        Assert.Equal("Batterie en avance au 2e couplet", edited.Body);
        Assert.NotNull(edited.EditedAt);

        Assert.Equal(HttpStatusCode.NoContent, (await a.Owner.DeleteAsync(url)).StatusCode);
        Assert.Empty(await ListAsync(a.Owner, a.Comments));
    }

    [Fact]
    public async Task Supprimer_un_fil_efface_ses_reponses()
    {
        var a = await ArrangeAsync();
        var comment = await PostAsync(a.Owner, a.Comments, new { body = "Tempo ?" });
        await PostAsync(a.Owner, $"{a.Comments}/{comment.Id}/replies", new { body = "92" });
        await PostAsync(a.Owner, $"{a.Comments}/{comment.Id}/replies", new { body = "96 plutôt" });

        var response = await a.Owner.DeleteAsync($"{a.Comments}/{comment.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await ListAsync(a.Owner, a.Comments));
    }

    [Fact]
    public async Task Supprimer_le_fichier_efface_ses_commentaires_et_leurs_reponses()
    {
        var a = await ArrangeAsync();
        var comment = await PostAsync(a.Owner, a.Comments, new { body = "Intro trop longue", positionMs = 1_000 });
        await PostAsync(a.Owner, $"{a.Comments}/{comment.Id}/replies", new { body = "D'accord" });

        Assert.Equal(HttpStatusCode.NoContent, (await a.Owner.DeleteAsync(a.File)).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await a.Owner.GetAsync(a.Comments)).StatusCode);
    }

    [Fact]
    public async Task On_ne_repond_pas_a_une_reponse()
    {
        var a = await ArrangeAsync();
        var comment = await PostAsync(a.Owner, a.Comments, new { body = "Basse" });
        var reply = await PostAsync(a.Owner, $"{a.Comments}/{comment.Id}/replies", new { body = "Oui" });

        var response = await a.Owner.PostAsJsonAsync($"{a.Comments}/{reply.Id}/replies", new { body = "Non" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Theory]
    [InlineData("", 1_000)]
    [InlineData("Avant le début", -1)]
    public async Task Texte_vide_ou_position_negative_renvoient_400(string body, int positionMs)
    {
        var a = await ArrangeAsync();

        var response = await a.Owner.PostAsJsonAsync(a.Comments, new { body, positionMs });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Une_partition_ne_se_commente_pas()
    {
        var a = await ArrangeAsync();
        var pdf = new byte[256];
        "%PDF-1.7"u8.CopyTo(pdf);
        var upload = await a.Owner.PostAsync($"{a.Version}/files", TestFiles.Form(pdf, "grille.pdf"));
        var file = (await upload.Content.ReadFromJsonAsync<SongFileResponse>(TestJson.Options))!;

        var response = await a.Owner.PostAsJsonAsync($"{a.Version}/files/{file.Id}/comments", new { body = "Mesure 12" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task Les_commentaires_d_un_autre_groupe_sont_introuvables()
    {
        var a = await ArrangeAsync();
        var comment = await PostAsync(a.Owner, a.Comments, new { body = "Privé" });
        var stranger = await factory.CreateNewUserClientAsync();
        var strangerBand = await stranger.FirstBandIdAsync();
        // Mêmes identifiants de morceau, version, fichier et commentaire, mais derrière le groupe de l'intrus
        var foreign = a.Comments.Replace($"/bands/{a.BandId}/", $"/bands/{strangerBand}/");

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync(a.Comments)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync(foreign)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsJsonAsync(foreign, new { body = "Intrus" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await stranger.PostAsJsonAsync($"{foreign}/{comment.Id}/replies", new { body = "Intrus" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.DeleteAsync($"{foreign}/{comment.Id}")).StatusCode);
    }
}