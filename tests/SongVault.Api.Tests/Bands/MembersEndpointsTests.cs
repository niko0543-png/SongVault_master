using System.Net;
using System.Net.Http.Json;

using SongVault.Api.Contracts.Bands;
using SongVault.Domain.Bands;

namespace SongVault.Api.Tests.Bands;

[Collection(ApiCollection.Name)]
public sealed class MembersEndpointsTests(SongVaultApiFactory factory)
{
    /// <summary>Un Owner et son groupe personnel, plus un second utilisateur ajouté avec le rôle demandé.</summary>
    private async Task<(HttpClient Owner, HttpClient Other, string Members)> ArrangeAsync(BandRole role)
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var other = await factory.CreateNewUserClientAsync();
        await factory.AddMemberAsync(bandId, other, role);
        return (owner, other, $"/api/bands/{bandId}/members");
    }

    private static async Task<List<BandMemberResponse>> ListAsync(HttpClient client, string members)
        => (await client.GetFromJsonAsync<List<BandMemberResponse>>(members, TestJson.Options))!;

    private static Task<HttpResponseMessage> ChangeRoleAsync(HttpClient client, string members, string userId, string role)
        => client.PutAsJsonAsync($"{members}/{userId}/role", new { role });

    [Fact]
    public async Task Tout_membre_voit_la_liste_avec_les_roles()
    {
        var (owner, guest, members) = await ArrangeAsync(BandRole.Guest);

        var ownerId = await owner.UserIdAsync();
        var guestId = await guest.UserIdAsync();

        var list = await ListAsync(guest, members);

        Assert.Equal(2, list.Count);
        Assert.Equal(BandRole.Owner, list.Single(m => m.UserId == ownerId).Role);
        Assert.Equal(BandRole.Guest, list.Single(m => m.UserId == guestId).Role);
        Assert.All(list, m => Assert.EndsWith("@test.local", m.Email));
    }

    [Fact]
    public async Task Owner_change_le_role_et_le_nouveau_role_s_applique()
    {
        var (owner, member, members) = await ArrangeAsync(BandRole.Member);
        var songs = members.Replace("/members", "/songs");

        var response = await ChangeRoleAsync(owner, members, await member.UserIdAsync(), "Guest");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PostAsJsonAsync(songs, new { title = "Refusé" })).StatusCode);
    }

    [Fact]
    public async Task Member_ne_peut_pas_changer_un_role()
    {
        var (owner, member, members) = await ArrangeAsync(BandRole.Member);

        var response = await ChangeRoleAsync(member, members, await owner.UserIdAsync(), "Guest");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Dernier_Owner_ne_peut_pas_se_retrograder()
    {
        var (owner, _, members) = await ArrangeAsync(BandRole.Member);

        var response = await ChangeRoleAsync(owner, members, await owner.UserIdAsync(), "Member");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Dernier_Owner_ne_peut_pas_quitter()
    {
        var (owner, _, members) = await ArrangeAsync(BandRole.Member);

        Assert.Equal(HttpStatusCode.Conflict, (await owner.DeleteAsync($"{members}/me")).StatusCode);
    }

    [Fact]
    public async Task Owner_peut_partir_apres_avoir_nomme_un_autre_Owner()
    {
        var (owner, member, members) = await ArrangeAsync(BandRole.Member);
        await ChangeRoleAsync(owner, members, await member.UserIdAsync(), "Owner");

        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"{members}/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync(members)).StatusCode);
    }

    [Fact]
    public async Task Guest_peut_quitter_le_groupe()
    {
        var (_, guest, members) = await ArrangeAsync(BandRole.Guest);

        Assert.Equal(HttpStatusCode.NoContent, (await guest.DeleteAsync($"{members}/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync(members)).StatusCode);
    }

    [Fact]
    public async Task Owner_retire_un_membre_qui_recoit_ensuite_404()
    {
        var (owner, member, members) = await ArrangeAsync(BandRole.Member);

        var response = await owner.DeleteAsync($"{members}/{await member.UserIdAsync()}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Single(await ListAsync(owner, members));
        Assert.Equal(HttpStatusCode.NotFound, (await member.GetAsync(members.Replace("/members", "/songs"))).StatusCode);
    }

    [Fact]
    public async Task Utilisateur_hors_du_groupe_renvoie_404()
    {
        var (owner, _, members) = await ArrangeAsync(BandRole.Member);

        Assert.Equal(HttpStatusCode.NotFound, (await ChangeRoleAsync(owner, members, "inconnu", "Guest")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.DeleteAsync($"{members}/inconnu")).StatusCode);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"role\":\"Admin\"}")]
    [InlineData("{\"role\":0}")]
    public async Task Role_absent_ou_inconnu_renvoie_400(string body)
    {
        var (owner, member, members) = await ArrangeAsync(BandRole.Member);
        var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");

        var response = await owner.PutAsync($"{members}/{await member.UserIdAsync()}/role", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Non_membre_recoit_404_sur_la_liste()
    {
        var (_, _, members) = await ArrangeAsync(BandRole.Member);
        var stranger = await factory.CreateNewUserClientAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync(members)).StatusCode);
    }
}