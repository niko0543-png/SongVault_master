using SongVault.Domain.Activity;
using SongVault.Domain.Common;
using SongVault.Domain.Files;
using SongVault.Domain.Songs;

namespace SongVault.Domain.Tests.Activity;

public sealed class ActivityEventTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid BandId = Guid.CreateVersion7();

    private static (Song Song, SongVersion Version) NewVersion()
    {
        var song = Song.Create(BandId, "Nocturne", null, null, Now);
        var version = song.AddVersion("Démo piano", SongVersionStatus.Demo, null, null, Now);
        return (song, version);
    }

    [Fact]
    public void ForSong_garde_le_groupe_l_auteur_et_le_titre_du_morceau()
    {
        var song = Song.Create(BandId, "Nocturne", null, null, Now);

        var e = ActivityEvent.ForSong(ActivityType.SongCreated, song, "ana", Now);

        Assert.Equal(BandId, e.BandId);
        Assert.Equal("ana", e.ActorId);
        Assert.Equal(song.Id, e.SongId);
        Assert.Equal("Nocturne", e.Details.SongTitle);
        Assert.Null(e.VersionId);
        Assert.Null(e.EntityId);
        Assert.Equal(Now, e.CreatedAt);
    }

    [Fact]
    public void ForVersion_garde_le_numero_et_le_titre_de_la_version()
    {
        var (song, version) = NewVersion();

        var e = ActivityEvent.ForVersion(ActivityType.VersionAdded, song, version, "ana", Now);

        Assert.Equal(version.Id, e.VersionId);
        Assert.Equal(1, e.Details.VersionNumber);
        Assert.Equal("Démo piano", e.Details.VersionTitle);
        Assert.Null(e.EntityId);
    }

    [Fact]
    public void ForFile_garde_le_nom_du_fichier()
    {
        var (song, version) = NewVersion();
        var file = version.AddFile("prise 3.wav", "k.wav", "audio/wav", 10, SongFileType.Audio, Now);

        var e = ActivityEvent.ForFile(ActivityType.FileAdded, song, version, file, "ana", Now);

        Assert.Equal(file.Id, e.EntityId);
        Assert.Equal("prise 3.wav", e.Details.FileName);
    }

    [Fact]
    public void Un_evenement_de_morceau_ignore_version_et_entite()
    {
        var e = ActivityEvent.Record(BandId, "ana", ActivityType.SongUpdated, Guid.CreateVersion7(),
            new ActivityDetails("Nocturne"), Now, versionId: Guid.CreateVersion7(), entityId: Guid.CreateVersion7());

        Assert.Null(e.VersionId);
        Assert.Null(e.EntityId);
    }

    [Theory]
    [InlineData(ActivityType.VersionAdded, false, false)]   // sans version
    [InlineData(ActivityType.FileAdded, true, false)]       // sans fichier
    [InlineData(ActivityType.CommentReplied, true, false)]  // sans commentaire
    public void Refuse_un_evenement_incomplet(ActivityType type, bool withVersion, bool withEntity)
    {
        Assert.Throws<DomainException>(() => ActivityEvent.Record(BandId, "ana", type, Guid.CreateVersion7(),
            new ActivityDetails("Nocturne"), Now,
            withVersion ? Guid.CreateVersion7() : null, withEntity ? Guid.CreateVersion7() : null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Refuse_un_evenement_sans_auteur(string actorId)
    {
        var song = Song.Create(BandId, "Nocturne", null, null, Now);

        Assert.Throws<DomainException>(() => ActivityEvent.ForSong(ActivityType.SongCreated, song, actorId, Now));
    }

    [Fact]
    public void Refuse_un_type_inconnu()
    {
        Assert.Throws<DomainException>(() => ActivityEvent.Record(BandId, "ana", (ActivityType)99,
            Guid.CreateVersion7(), new ActivityDetails("Nocturne"), Now));
    }

    [Fact]
    public void SongView_refuse_un_utilisateur_vide()
    {
        Assert.Throws<DomainException>(() => SongView.Create(" ", Guid.CreateVersion7(), Now));
    }
}