using System.Net;
using System.Net.Http.Json;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using SongVault.Api.Contracts.Audit;
using SongVault.Api.Contracts.Auth;
using SongVault.Domain.Audit;
using SongVault.Domain.Bands;
using SongVault.Infrastructure.Persistence;

namespace SongVault.Api.Tests.Audit;

[Collection(ApiCollection.Name)]
[Trait("Category", "Security")]
public sealed class AuditEndpointsTests(SongVaultApiFactory factory)
{
    private static async Task<AuditPageResponse> AuditAsync(HttpClient client, Guid bandId, string query = "")
        => (await client.GetFromJsonAsync<AuditPageResponse>($"/api/bands/{bandId}/audit{query}", TestJson.Options))!;

    private async Task<(HttpClient Owner, HttpClient Member, Guid BandId)> BandWithMemberAsync(BandRole role = BandRole.Member)
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        var member = await factory.CreateNewUserClientAsync();
        await factory.AddMemberAsync(bandId, member, role);
        return (owner, member, bandId);
    }

    private static async Task<string> EmailAsync(HttpClient client)
        => (await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me", TestJson.Options))!.Email;

    [Fact]
    public async Task L_Owner_voit_qui_a_supprime_quel_morceau()
    {
        var (owner, member, bandId) = await BandWithMemberAsync();
        var created = await owner.PostAsJsonAsync($"/api/bands/{bandId}/songs", new { title = "Nocturne" });
        Assert.Equal(HttpStatusCode.NoContent, (await member.DeleteAsync(created.Headers.Location)).StatusCode);

        var entry = Assert.Single((await AuditAsync(owner, bandId)).Items);
        Assert.Equal((AuditAction.SongDeleted, "Nocturne"), (entry.Action, entry.Subject));
        Assert.Equal(await EmailAsync(member), entry.ActorEmail);
    }

    [Theory]
    [InlineData(BandRole.Member)]
    [InlineData(BandRole.Guest)]
    public async Task Seul_l_Owner_lit_le_journal(BandRole role)
    {
        var (_, member, bandId) = await BandWithMemberAsync(role);

        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync($"/api/bands/{bandId}/audit")).StatusCode);
    }

    [Fact]
    public async Task Changer_un_role_puis_retirer_un_membre_est_note_avec_le_compte_vise()
    {
        var (owner, member, bandId) = await BandWithMemberAsync();
        var memberId = await member.UserIdAsync();

        await owner.PutAsJsonAsync($"/api/bands/{bandId}/members/{memberId}/role", new { role = "Guest" }, TestJson.Options);
        await owner.DeleteAsync($"/api/bands/{bandId}/members/{memberId}");

        var items = (await AuditAsync(owner, bandId)).Items;
        Assert.Equal([AuditAction.MemberRemoved, AuditAction.MemberRoleChanged], items.Select(i => i.Action));
        Assert.All(items, i => Assert.Equal(memberId, i.TargetUserId));
        Assert.Equal("Member → Guest", items[1].Details);
        Assert.Equal(await EmailAsync(member), items[1].TargetEmail);
    }

    [Fact]
    public async Task Le_journal_se_lit_page_par_page()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        foreach (var name in new[] { "Un", "Deux", "Trois" })
            await owner.PutAsJsonAsync($"/api/bands/{bandId}", new { name });

        var first = await AuditAsync(owner, bandId, "?pageSize=2");
        var second = await AuditAsync(owner, bandId, $"?pageSize=2&cursor={first.NextCursor}");

        Assert.Equal(["Trois", "Deux"], first.Items.Select(i => i.Subject));
        Assert.Equal(["Un"], second.Items.Select(i => i.Subject));
        Assert.Null(second.NextCursor);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.GetAsync($"/api/bands/{bandId}/audit?cursor=nimporte")).StatusCode);
    }

    [Fact]
    public async Task Le_journal_ne_se_modifie_ni_par_EF_ni_en_SQL()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        await owner.PutAsJsonAsync($"/api/bands/{bandId}", new { name = "Les Rats" });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SongVaultDbContext>();

        var entry = await db.AuditEntries.SingleAsync(e => e.BandId == bandId);
        db.AuditEntries.Remove(entry);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        var update = await Assert.ThrowsAsync<SqlException>(() =>
            db.Database.ExecuteSqlAsync($"UPDATE AuditEntries SET Subject = N'effacé' WHERE BandId = {bandId}"));
        Assert.Equal(51000, update.Number);
        var delete = await Assert.ThrowsAsync<SqlException>(() =>
            db.AuditEntries.Where(e => e.BandId == bandId).ExecuteDeleteAsync());
        Assert.Equal(51000, delete.Number);

        Assert.Equal("Les Rats", (await db.AuditEntries.AsNoTracking().SingleAsync(e => e.BandId == bandId)).Subject);
    }

    [Fact]
    public async Task Le_journal_part_avec_son_groupe_ou_au_bout_d_un_an()
    {
        var owner = await factory.CreateNewUserClientAsync();
        var bandId = await owner.FirstBandIdAsync();
        await owner.PutAsJsonAsync($"/api/bands/{bandId}", new { name = "Les Rats" });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SongVaultDbContext>();

        // Une ligne d'il y a 400 jours (l'insertion reste permise) : la rétention peut l'effacer
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO AuditEntries (BandId, ActorId, Action, OccurredAt)
            VALUES ({bandId}, NULL, N'BandRenamed', DATEADD(day, -400, SYSDATETIMEOFFSET()))
            """);
        var old = DateTimeOffset.UtcNow.AddDays(-365);
        Assert.Equal(1, await db.AuditEntries.Where(e => e.BandId == bandId && e.OccurredAt < old).ExecuteDeleteAsync());

        // Le groupe effacé (purge d'un compte) : son journal part avec lui, en cascade
        await db.Bands.Where(b => b.Id == bandId).ExecuteDeleteAsync();
        Assert.False(await db.AuditEntries.AnyAsync(e => e.BandId == bandId));
    }
}