using SongVault.Application.Activity;
using SongVault.Application.Audit;
using SongVault.Application.Tests.Fakes;
using SongVault.Domain.Audit;
using SongVault.Domain.Bands;

namespace SongVault.Application.Tests.Audit;

public sealed class AuditHandlersTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);
    private readonly FakeAuditRepository _entries = new();

    private AuditItemDto Item(long id, DateTimeOffset at)
        => new(id, AuditAction.SongDeleted, "ana", "ana@test.local", $"Morceau {id}", null, null, null, at);

    [Fact]
    public void AuditLog_ecrit_au_nom_de_l_utilisateur_de_la_requete()
    {
        var bandId = Guid.NewGuid();
        new AuditLog(_entries, new FakeCurrentUser("ana")).Record(bandId, AuditAction.MemberRoleChanged, null, Now,
            "bob", AuditLog.RoleChange(BandRole.Member, BandRole.Owner));

        var entry = Assert.Single(_entries.Entries);
        Assert.Equal((bandId, "ana", "bob", "Member → Owner", Now),
            (entry.BandId, entry.ActorId, entry.TargetUserId, entry.Details, entry.OccurredAt));
    }

    [Fact]
    public async Task Le_journal_se_lit_page_par_page_sans_doublon()
    {
        for (var i = 1; i <= 5; i++) _entries.Items.Add(Item(i, Now.AddMinutes(i)));
        _entries.Items.Add(Item(6, Now.AddMinutes(5)));                 // même date que la 5 : départagée par le numéro
        var handler = new ListAuditHandler(_entries);

        var first = await handler.HandleAsync(null, 4, CancellationToken.None);
        Assert.True(ActivityCursor.TryParse(first.NextCursor, out var cursor));
        var second = await handler.HandleAsync(cursor, 4, CancellationToken.None);

        Assert.Equal([6L, 5, 4, 3], first.Items.Select(i => i.Id));
        Assert.Equal([2L, 1], second.Items.Select(i => i.Id));
        Assert.Null(second.NextCursor);
    }

    [Fact]
    public async Task La_taille_de_page_est_bornee()
    {
        for (var i = 1; i <= ListAuditHandler.MaxPageSize + 5; i++) _entries.Items.Add(Item(i, Now.AddSeconds(i)));

        var page = await new ListAuditHandler(_entries).HandleAsync(null, 1000, CancellationToken.None);

        Assert.Equal(ListAuditHandler.MaxPageSize, page.Items.Count);
        Assert.NotNull(page.NextCursor);
    }
}