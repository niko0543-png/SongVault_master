using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using SongVault.Api.Contracts.Auth;
using SongVault.Api.Contracts.Bands;
using SongVault.Api.Contracts.Invitations;
using SongVault.Domain.Bands;
using SongVault.Infrastructure.Persistence;

namespace SongVault.Api.Tests.Invitations;

[Collection(ApiCollection.Name)]
public sealed class InvitationsEndpointsTests(SongVaultApiFactory factory)
{
    private static string NewEmail() => $"invite-{Guid.NewGuid():N}@test.local";
    private static string TokenOf(string link) => link[(link.LastIndexOf('/') + 1)..];

    private static Task<HttpResponseMessage> PostInvitationAsync(HttpClient owner, Guid bandId, string email, string role = "Member")
        => owner.PostAsJsonAsync($"/api/bands/{bandId}/invitations", new { email, role });

    /// <summary>Un nouvel Owner invite une adresse neuve dans son groupe personnel.</summary>
    private async Task<(HttpClient Owner, Guid BandId, string Email, InvitationCreatedResponse Created)> InviteAsync(
        string role = "Member")
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var email = NewEmail();

        var response = await PostInvitationAsync(owner, bandId, email, role);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (owner, bandId, email, (await response.Content.ReadFromJsonAsync<InvitationCreatedResponse>(TestJson.Options))!);
    }

    private static Task<HttpResponseMessage> AcceptAsync(HttpClient client, string link)
        => client.PostAsync($"/api/invitations/{TokenOf(link)}/accept", null);

    private async Task ExpireAsync(Guid invitationId)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<SongVaultDbContext>().Invitations
            .Where(i => i.Id == invitationId)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
    }

    [Fact]
    public async Task Invitation_envoie_un_email_qui_contient_le_lien()
    {
        var (_, _, email, created) = await InviteAsync();

        Assert.True(created.EmailSent);
        Assert.StartsWith("http://localhost:8080/invite/", created.Link);
        Assert.Contains(created.Link, factory.Emails.LastTo(email).TextBody);
    }

    [Fact]
    public async Task La_base_ne_contient_que_l_empreinte_du_jeton()
    {
        var (_, _, _, created) = await InviteAsync();
        var token = TokenOf(created.Link);

        using var scope = factory.Services.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<SongVaultDbContext>()
            .Invitations.AsNoTracking().SingleAsync(i => i.Id == created.Id);

        Assert.Equal(32, stored.TokenHash.Length);
        Assert.Equal(SHA256.HashData(Encoding.UTF8.GetBytes(token)), stored.TokenHash);
    }

    [Fact]
    public async Task Apercu_accessible_sans_etre_connecte()
    {
        var (_, _, email, created) = await InviteAsync("Guest");
        var anonymous = factory.CreateClient(new() { HandleCookies = false });

        var preview = (await anonymous.GetFromJsonAsync<InvitationPreviewResponse>(
            $"/api/invitations/{TokenOf(created.Link)}", TestJson.Options))!;

        Assert.StartsWith("Groupe de user-", preview.BandName);
        Assert.Equal(email, preview.Email);
        Assert.Equal(BandRole.Guest, preview.Role);
    }

    [Fact]
    public async Task Sans_compte_inscription_puis_adhesion_avec_le_role_prevu()
    {
        var (_, bandId, email, created) = await InviteAsync();

        var invited = await factory.CreateNewUserClientAsync(email);     // inscription avec l'adresse invitée
        var response = await AcceptAsync(invited, created.Link);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var band = (await response.Content.ReadFromJsonAsync<BandResponse>(TestJson.Options))!;
        Assert.Equal(bandId, band.Id);
        Assert.Equal(BandRole.Member, band.Role);
        // Membre d'un groupe dès son premier GET /api/bands : pas de groupe personnel en plus
        var mine = Assert.Single((await invited.GetFromJsonAsync<List<BandResponse>>("/api/bands", TestJson.Options))!);
        Assert.Equal(bandId, mine.Id);
    }

    [Fact]
    public async Task Lien_deja_utilise_renvoie_410_a_un_autre_compte()
    {
        var (_, _, email, created) = await InviteAsync();
        await AcceptAsync(await factory.CreateNewUserClientAsync(email), created.Link);

        var other = await factory.CreateNewUserClientAsync();

        Assert.Equal(HttpStatusCode.Gone, (await AcceptAsync(other, created.Link)).StatusCode);
    }

    [Fact]
    public async Task Meme_compte_qui_rejoue_le_lien_retrouve_son_groupe()
    {
        var (_, bandId, email, created) = await InviteAsync();
        var invited = await factory.CreateNewUserClientAsync(email);
        await AcceptAsync(invited, created.Link);

        var again = await AcceptAsync(invited, created.Link);

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(bandId, (await again.Content.ReadFromJsonAsync<BandResponse>(TestJson.Options))!.Id);
    }

    [Fact]
    public async Task Lien_expire_renvoie_410()
    {
        var (_, _, email, created) = await InviteAsync();
        await ExpireAsync(created.Id);
        var invited = await factory.CreateNewUserClientAsync(email);

        Assert.Equal(HttpStatusCode.Gone, (await invited.GetAsync($"/api/invitations/{TokenOf(created.Link)}")).StatusCode);
        Assert.Equal(HttpStatusCode.Gone, (await AcceptAsync(invited, created.Link)).StatusCode);
    }

    [Fact]
    public async Task Invitation_annulee_renvoie_410_et_sort_de_la_liste()
    {
        var (owner, bandId, email, created) = await InviteAsync();

        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.DeleteAsync($"/api/bands/{bandId}/invitations/{created.Id}")).StatusCode);

        Assert.Empty((await owner.GetFromJsonAsync<List<InvitationResponse>>($"/api/bands/{bandId}/invitations", TestJson.Options))!);
        Assert.Equal(HttpStatusCode.Gone, (await AcceptAsync(await factory.CreateNewUserClientAsync(email), created.Link)).StatusCode);
    }

    [Fact]
    public async Task Reinviter_la_meme_adresse_annule_le_lien_precedent()
    {
        var (owner, bandId, email, first) = await InviteAsync();

        var second = (await (await PostInvitationAsync(owner, bandId, email))
            .Content.ReadFromJsonAsync<InvitationCreatedResponse>(TestJson.Options))!;

        var anonymous = factory.CreateClient(new() { HandleCookies = false });
        Assert.Equal(HttpStatusCode.Gone, (await anonymous.GetAsync($"/api/invitations/{TokenOf(first.Link)}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync($"/api/invitations/{TokenOf(second.Link)}")).StatusCode);
        Assert.Single((await owner.GetFromJsonAsync<List<InvitationResponse>>($"/api/bands/{bandId}/invitations", TestJson.Options))!);
    }

    [Fact]
    public async Task Compte_avec_une_autre_adresse_recoit_403()
    {
        var (_, _, _, created) = await InviteAsync();
        var other = await factory.CreateNewUserClientAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await AcceptAsync(other, created.Link)).StatusCode);
    }

    [Fact]
    public async Task Lien_inconnu_renvoie_404()
    {
        var anonymous = factory.CreateClient(new() { HandleCookies = false });

        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/api/invitations/inconnu")).StatusCode);
    }

    [Fact]
    public async Task Member_ne_peut_ni_inviter_ni_voir_les_invitations()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var member = await factory.CreateNewUserClientAsync();
        await factory.AddMemberAsync(bandId, member, BandRole.Member);

        Assert.Equal(HttpStatusCode.Forbidden, (await PostInvitationAsync(member, bandId, NewEmail())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync($"/api/bands/{bandId}/invitations")).StatusCode);
    }

    [Theory]
    [InlineData("Owner", HttpStatusCode.UnprocessableEntity)]
    [InlineData("Admin", HttpStatusCode.BadRequest)]
    public async Task Role_Owner_ou_inconnu_refuse(string role, HttpStatusCode expected)
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();

        Assert.Equal(expected, (await PostInvitationAsync(owner, bandId, NewEmail(), role)).StatusCode);
    }

    [Fact]
    public async Task Adresse_invalide_renvoie_400()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await PostInvitationAsync(owner, bandId, "pas-une-adresse")).StatusCode);
    }

    [Fact]
    public async Task Un_membre_ne_peut_pas_etre_reinvite()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var me = (await owner.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me", TestJson.Options))!;

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PostInvitationAsync(owner, bandId, me.Email)).StatusCode);
    }

    [Fact]
    public async Task Deux_acceptations_simultanees_ne_creent_qu_une_adhesion()
    {
        var (owner, bandId, email, created) = await InviteAsync();
        var invited = await factory.CreateNewUserClientAsync(email);

        var responses = await Task.WhenAll(AcceptAsync(invited, created.Link), AcceptAsync(invited, created.Link));

        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.All(responses, r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }));
        var members = (await owner.GetFromJsonAsync<List<BandMemberResponse>>($"/api/bands/{bandId}/members", TestJson.Options))!;
        Assert.Equal(2, members.Count);
    }
}