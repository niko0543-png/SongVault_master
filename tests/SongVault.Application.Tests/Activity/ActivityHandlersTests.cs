using SongVault.Application.Activity;
using SongVault.Application.Common.Exceptions;
using SongVault.Application.Songs.CreateSong;
using SongVault.Application.Songs.DeleteSong;
using SongVault.Application.Songs.UpdateSong;
using SongVault.Application.Tests.Fakes;
using SongVault.Application.Versions;
using SongVault.Domain.Activity;
using SongVault.Domain.Common;
using SongVault.Domain.Songs;

namespace SongVault.Application.Tests.Activity;

public sealed class ActivityHandlersTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid BandId = Guid.CreateVersion7();
    private readonly FakeSongRepository _songs = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeActivityRepository _activity = new();
    private readonly FixedTimeProvider _clock = new(Now);

    private ActivityLog Log(string userId = "ana") => new(_activity, new FakeCurrentUser(userId));

    private Song ExistingSong()
    {
        var song = Song.Create(BandId, "Nocturne", null, null, Now.AddDays(-1));
        _songs.Songs.Add(song);
        return song;
    }

    [Fact]
    public async Task Creer_un_morceau_ecrit_l_evenement_avec_le_meme_enregistrement()
    {
        var created = await new CreateSongHandler(_songs, _uow, new FakeBandContext(BandId), _clock, Log())
            .HandleAsync(new CreateSongCommand("Nocturne", null, null), CancellationToken.None);

        var e = Assert.Single(_activity.Events);
        Assert.Equal(ActivityType.SongCreated, e.Type);
        Assert.Equal(created.Id, e.SongId);
        Assert.Equal(BandId, e.BandId);
        Assert.Equal("ana", e.ActorId);
        Assert.Equal(Now, e.CreatedAt);
        Assert.Equal(1, _uow.SaveCount);                     // un seul SaveChangesAsync : une seule transaction
    }

    [Fact]
    public async Task Une_modification_refusee_n_ecrit_aucun_evenement()
    {
        var song = ExistingSong();

        await Assert.ThrowsAsync<DomainException>(() => new UpdateSongHandler(_songs, _uow, _clock, Log())
            .HandleAsync(new UpdateSongCommand(song.Id, " ", null, null), CancellationToken.None));

        Assert.Empty(_activity.Events);
    }

    [Fact]
    public async Task Ajouter_une_version_note_son_numero()
    {
        var song = ExistingSong();
        song.AddVersion("Idée", SongVersionStatus.Idea, null, null, Now.AddDays(-1));

        await new CreateSongVersionHandler(_songs, _uow, _clock, Log())
            .HandleAsync(new CreateSongVersionCommand(song.Id, "Démo", SongVersionStatus.Demo, null, null),
                CancellationToken.None);

        var e = Assert.Single(_activity.Events);
        Assert.Equal(ActivityType.VersionAdded, e.Type);
        Assert.Equal(2, e.Details.VersionNumber);
        Assert.Equal("Démo", e.Details.VersionTitle);
        Assert.NotNull(e.VersionId);
    }

    [Fact]
    public async Task Supprimer_un_morceau_garde_son_titre_dans_le_fil()
    {
        var song = ExistingSong();

        await new DeleteSongHandler(_songs, new FakeReleaseRepository(), new FakeSetlistRepository(), _uow, new FakeCurrentUser("ana"), _clock, Log(),
        new SongVault.Application.Audit.AuditLog(new FakeAuditRepository(), new FakeCurrentUser("ana")))
            .HandleAsync(song.Id, CancellationToken.None);

        var e = Assert.Single(_activity.Events);
        Assert.Equal(ActivityType.SongDeleted, e.Type);
        Assert.Equal("Nocturne", e.Details.SongTitle);
    }

    private static ActivityItemDto Item(long id, DateTimeOffset at)
        => new(id, ActivityType.SongUpdated, "ana", "ana@test.local", Guid.CreateVersion7(), true,
            null, null, "Nocturne", null, null, null, at);

    [Fact]
    public async Task Le_fil_se_lit_page_par_page_sans_doublon_ni_trou()
    {
        // 5 événements, dont deux à la même date : l'Id les départage
        _activity.Items.AddRange([Item(1, Now), Item(2, Now.AddMinutes(1)), Item(3, Now.AddMinutes(1)),
            Item(4, Now.AddMinutes(2)), Item(5, Now.AddMinutes(3))]);
        var handler = new ListActivityHandler(_activity);

        var first = await handler.HandleAsync(null, 2, CancellationToken.None);
        Assert.True(ActivityCursor.TryParse(first.NextCursor, out var c1));
        var second = await handler.HandleAsync(c1, 2, CancellationToken.None);
        Assert.True(ActivityCursor.TryParse(second.NextCursor, out var c2));
        var third = await handler.HandleAsync(c2, 2, CancellationToken.None);

        Assert.Equal(new long[] { 5, 4 }, first.Items.Select(i => i.Id));
        Assert.Equal(new long[] { 3, 2 }, second.Items.Select(i => i.Id));
        Assert.Equal(new long[] { 1 }, third.Items.Select(i => i.Id));
        Assert.Null(third.NextCursor);
    }

    [Fact]
    public async Task Un_evenement_arrive_entre_deux_pages_ne_decale_pas_la_suite()
    {
        _activity.Items.AddRange([Item(1, Now), Item(2, Now.AddMinutes(1)), Item(3, Now.AddMinutes(2))]);
        var handler = new ListActivityHandler(_activity);
        var first = await handler.HandleAsync(null, 2, CancellationToken.None);

        _activity.Items.Add(Item(4, Now.AddMinutes(5)));                // arrivé pendant la lecture
        ActivityCursor.TryParse(first.NextCursor, out var cursor);
        var second = await handler.HandleAsync(cursor, 2, CancellationToken.None);

        Assert.Equal(new long[] { 1 }, second.Items.Select(i => i.Id));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("12_")]
    [InlineData("-5_3")]
    [InlineData("1_2_3")]
    public void Un_curseur_mal_forme_est_refuse(string? text)
    {
        Assert.False(ActivityCursor.TryParse(text, out _));
    }

    [Fact]
    public void Un_curseur_se_relit_a_l_identique()
    {
        var cursor = new ActivityCursor(Now.AddTicks(7), 42);

        Assert.True(ActivityCursor.TryParse(cursor.ToString(), out var read));
        Assert.Equal(cursor, read);
    }

    [Fact]
    public async Task Ouvrir_un_morceau_inconnu_leve_NotFound()
    {
        var views = new FakeSongViewRepository();

        await Assert.ThrowsAsync<NotFoundException>(() => new MarkSongSeenHandler(_songs, views, new FakeCurrentUser(), _clock)
            .HandleAsync(Guid.NewGuid(), CancellationToken.None));
        Assert.Empty(views.SeenAt);
    }

    [Fact]
    public async Task Ouvrir_un_morceau_note_la_date_pour_cet_utilisateur()
    {
        var song = ExistingSong();
        var views = new FakeSongViewRepository();

        await new MarkSongSeenHandler(_songs, views, new FakeCurrentUser("ana"), _clock)
            .HandleAsync(song.Id, CancellationToken.None);

        Assert.Equal(Now, views.SeenAt[(song.Id, "ana")]);
    }
}