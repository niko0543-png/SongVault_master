using SongVault.Domain.Common;
using SongVault.Domain.Files;
using SongVault.Domain.Songs;
using SongVault.Domain.Trash;

namespace SongVault.Domain.Tests.Songs;

/// <summary>Corbeille (#20) : ce qui part ensemble revient ensemble, et rien d'autre.</summary>
public sealed class SongTrashTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 10, 0, 0, TimeSpan.Zero);

    private static (Song Song, SongVersion V1, SongVersion V2) SongWithTwoVersions()
    {
        var song = Song.Create(Guid.NewGuid(), "Nocturne", null, null, Now);
        var v1 = song.AddVersion("Idée", SongVersionStatus.Idea, null, null, Now);
        v1.AddFile("idee.m4a", "k1", "audio/mp4", 10, SongFileType.Audio, Now);
        var v2 = song.AddVersion("Maquette", SongVersionStatus.Demo, null, null, Now);
        v2.AddFile("maquette.wav", "k2", "audio/wav", 20, SongFileType.Audio, Now);
        v2.AddFile("paroles.pdf", "k3", "application/pdf", 5, SongFileType.Document, Now);
        return (song, v1, v2);
    }

    [Fact]
    public void MoveToTrash_marque_le_morceau_ses_versions_et_leurs_fichiers_avec_la_meme_date()
    {
        var (song, v1, v2) = SongWithTwoVersions();

        song.MoveToTrash("ana", Now.AddHours(1));

        ISoftDeletable[] all = [song, v1, v2, .. v1.Files, .. v2.Files];
        Assert.All(all, x => Assert.Equal((Now.AddHours(1), "ana"), (x.DeletedAt, x.DeletedById)));
    }

    [Fact]
    public void Restore_remet_tout_ce_qui_est_parti_avec_le_morceau()
    {
        var (song, v1, v2) = SongWithTwoVersions();
        song.MoveToTrash("ana", Now.AddHours(1));

        song.Restore();

        ISoftDeletable[] all = [song, v1, v2, .. v1.Files, .. v2.Files];
        Assert.All(all, x => Assert.Null(x.DeletedAt));
        Assert.All(all, x => Assert.Null(x.DeletedById));
    }

    [Fact]
    public void Restore_laisse_a_la_corbeille_ce_qui_y_etait_avant_le_morceau()
    {
        var (song, v1, v2) = SongWithTwoVersions();
        song.TrashVersion(v1.Id, "ana", Now.AddHours(1));
        var pdf = v2.Files.Single(f => f.FileType == SongFileType.Document);
        v2.TrashFile(pdf.Id, "ana", Now.AddHours(2));
        song.MoveToTrash("leo", Now.AddHours(3));

        song.Restore();

        Assert.False(v2.IsDeleted);
        Assert.False(v2.Files.Single(f => f.FileType == SongFileType.Audio).IsDeleted);
        Assert.Equal((Now.AddHours(1), "ana"), (v1.DeletedAt, v1.DeletedById));         // supprimée avant : elle reste
        Assert.Equal(Now.AddHours(1), v1.Files.Single().DeletedAt);
        Assert.Equal(Now.AddHours(2), pdf.DeletedAt);
    }

    [Fact]
    public void Un_morceau_ne_va_pas_deux_fois_a_la_corbeille_et_ne_revient_que_s_il_y_est()
    {
        var (song, _, _) = SongWithTwoVersions();

        Assert.Throws<DomainException>(song.Restore);
        song.MoveToTrash("ana", Now);
        Assert.Throws<DomainException>(() => song.MoveToTrash("ana", Now));
    }

    [Fact]
    public void TrashVersion_puis_RestoreVersion_ramenent_la_version_et_ses_fichiers()
    {
        var (song, _, v2) = SongWithTwoVersions();

        Assert.Same(v2, song.TrashVersion(v2.Id, "ana", Now));
        Assert.All(v2.Files, f => Assert.True(f.IsDeleted));
        Assert.Null(song.TrashVersion(v2.Id, "ana", Now));                               // déjà partie

        Assert.Same(v2, song.RestoreVersion(v2.Id));
        Assert.All(v2.Files, f => Assert.False(f.IsDeleted));
        Assert.Null(song.RestoreVersion(v2.Id));                                         // déjà revenue
    }

    [Fact]
    public void Une_version_ne_revient_pas_seule_tant_que_son_morceau_est_a_la_corbeille()
    {
        var (song, v1, _) = SongWithTwoVersions();
        song.TrashVersion(v1.Id, "ana", Now);
        song.MoveToTrash("ana", Now.AddHours(1));

        Assert.Throws<DomainException>(() => song.RestoreVersion(v1.Id));
        Assert.Null(song.TrashVersion(v1.Id, "ana", Now));
    }

    [Fact]
    public void Un_fichier_ne_revient_pas_seul_tant_que_sa_version_est_a_la_corbeille()
    {
        var (song, _, v2) = SongWithTwoVersions();
        var wav = v2.Files.First();
        v2.TrashFile(wav.Id, "ana", Now);
        Assert.Null(v2.TrashFile(wav.Id, "ana", Now));                                   // déjà parti
        song.TrashVersion(v2.Id, "ana", Now.AddHours(1));

        Assert.Throws<DomainException>(() => v2.RestoreFile(wav.Id));
        song.RestoreVersion(v2.Id);
        Assert.True(wav.IsDeleted);                                                      // parti avant la version
        Assert.Same(wav, v2.RestoreFile(wav.Id));
        Assert.False(wav.IsDeleted);
    }

    [Fact]
    public void La_purge_tombe_trente_jours_apres_la_suppression()
    {
        Assert.Equal(TimeSpan.FromDays(30), TrashPolicy.Retention);
        Assert.Equal(Now.AddDays(30), TrashPolicy.PurgeAt(Now));
    }
}