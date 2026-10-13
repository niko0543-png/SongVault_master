using SongVault.Domain.Comments;
using SongVault.Domain.Common;

namespace SongVault.Domain.Tests.Comments;

public sealed class CommentTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid BandId = Guid.CreateVersion7();
    private static readonly Guid FileId = Guid.CreateVersion7();

    private static Comment New(int? positionMs = 102_000, string body = "  À 1:42, la basse décale  ")
        => Comment.Start(BandId, FileId, "ana", positionMs, body, Now);

    [Fact]
    public void Start_garde_la_position_et_nettoie_le_texte()
    {
        var comment = New();

        Assert.Equal(102_000, comment.PositionMs);
        Assert.Equal("À 1:42, la basse décale", comment.Body);
        Assert.False(comment.IsReply);
        Assert.True(comment.IsWrittenBy("ana"));
        Assert.Null(comment.EditedAt);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(Comment.MaxPositionMs + 1)]
    public void Start_refuse_une_position_hors_limites(int positionMs)
        => Assert.Throws<DomainException>(() => New(positionMs));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Start_refuse_un_texte_vide(string body)
        => Assert.Throws<DomainException>(() => New(body: body));

    [Fact]
    public void Start_refuse_un_texte_trop_long()
        => Assert.Throws<DomainException>(() => New(body: new string('a', Comment.BodyMaxLength + 1)));

    [Fact]
    public void Reply_rattache_la_reponse_au_fil_sans_position()
    {
        var comment = New();

        var reply = comment.Reply("bob", "Corrigé dans la v3", Now.AddMinutes(5));

        Assert.Equal(comment.Id, reply.ParentId);
        Assert.Equal(FileId, reply.SongFileId);
        Assert.Equal(BandId, reply.BandId);
        Assert.Null(reply.PositionMs);
        Assert.True(reply.IsReply);
    }

    [Fact]
    public void On_ne_repond_pas_a_une_reponse()
    {
        var reply = New().Reply("bob", "Oui", Now);

        Assert.Throws<DomainException>(() => reply.Reply("ana", "Non", Now));
    }

    [Fact]
    public void Resolve_garde_la_premiere_resolution_et_Reopen_l_efface()
    {
        var comment = New();

        comment.Resolve("bob", Now);
        comment.Resolve("ana", Now.AddHours(1));

        Assert.Equal(Now, comment.ResolvedAt);
        Assert.Equal("bob", comment.ResolvedById);

        comment.Reopen();
        Assert.Null(comment.ResolvedAt);
        Assert.Null(comment.ResolvedById);
    }

    [Fact]
    public void Une_reponse_ne_se_resout_pas()
    {
        var reply = New().Reply("bob", "Oui", Now);

        Assert.Throws<DomainException>(() => reply.Resolve("bob", Now));
        Assert.Throws<DomainException>(() => reply.Reopen());
    }

    [Fact]
    public void Edit_change_le_texte_et_note_la_date()
    {
        var comment = New();

        comment.Edit("À 1:43 plutôt", Now.AddMinutes(1));

        Assert.Equal("À 1:43 plutôt", comment.Body);
        Assert.Equal(Now.AddMinutes(1), comment.EditedAt);
    }
}