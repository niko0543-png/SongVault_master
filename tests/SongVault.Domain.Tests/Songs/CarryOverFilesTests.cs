using SongVault.Domain.Common;
using SongVault.Domain.Files;
using SongVault.Domain.Songs;

namespace SongVault.Domain.Tests.Songs;

/// <summary>ADR 0046 : une nouvelle version reprend des fichiers d'une version précédente.</summary>
public sealed class CarryOverFilesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Now.AddDays(1);

    private readonly Song _song = Song.Create(Guid.NewGuid(), "Nocturne", null, null, Now);
    private readonly SongVersion _v1;
    private readonly SongFile _mix, _drums, _tab, _pdf;

    public CarryOverFilesTests()
    {
        _v1 = _song.AddVersion("v1", SongVersionStatus.Demo, null, null, Now);
        _mix = _v1.AddFile("mix.mp3", "a.mp3", "audio/mpeg", 100, SongFileType.Audio, Now);
        _drums = _v1.AddFile("batterie.mp3", "b.mp3", "audio/mpeg", 200, SongFileType.Audio, Now);
        _tab = _v1.AddFile("riff.gp", "c.gp", "application/octet-stream", 300, SongFileType.Tablature, Now);
        _pdf = _v1.AddFile("paroles.pdf", "d.pdf", "application/pdf", 400, SongFileType.Document, Now);
        _v1.SetStems([new StemChoice(_drums.Id, "Batterie"), new StemChoice(_mix.Id, "Mix")], Now);
        _v1.SetTablatureSync(_tab.Id, _mix.Id, [new TabSyncPoint(0, 0, 0, 0), new TabSyncPoint(1, 0, 0, 2000)], Now);
    }

    [Fact]
    public void Copie_les_fichiers_choisis_sous_leurs_nouvelles_cles_dans_l_ordre_d_origine()
    {
        var v2 = _song.AddVersion("v2", SongVersionStatus.Demo, null, null, Later);

        var copies = v2.CarryOverFiles(_v1, new Dictionary<Guid, string> { [_pdf.Id] = "y.pdf", [_mix.Id] = "x.mp3" }, Later);

        Assert.Equal(["mix.mp3", "paroles.pdf"], copies.Select(f => f.OriginalFileName));
        Assert.Equal(["x.mp3", "y.pdf"], copies.Select(f => f.StorageKey));
        Assert.All(copies, f => Assert.Equal(v2.Id, f.SongVersionId));
        Assert.Equal([100L, 400L], copies.Select(f => f.SizeBytes));
        Assert.Equal(Later, copies[0].UploadedAt);
        Assert.Equal(4, _v1.Files.Count);                                // l'original ne bouge pas
    }

    [Fact]
    public void Les_pistes_et_le_calage_suivent_quand_tous_leurs_fichiers_sont_repris()
    {
        var v2 = _song.AddVersion("v2", SongVersionStatus.Demo, null, null, Later);

        var copies = v2.CarryOverFiles(_v1,
            new Dictionary<Guid, string> { [_mix.Id] = "x.mp3", [_drums.Id] = "y.mp3", [_tab.Id] = "z.gp" }, Later);

        var (mix, drums, tab) = (copies[0], copies[1], copies[2]);
        Assert.Equal(("Batterie", 0), (drums.StemName, drums.StemPosition));
        Assert.Equal(("Mix", 1), (mix.StemName, mix.StemPosition));
        Assert.Equal(mix.Id, tab.SyncAudioFileId);
        Assert.Equal(2, tab.SyncPoints!.Count);
    }

    [Fact]
    public void Sans_sa_maquette_la_tablature_n_est_pas_calee_et_les_pistes_se_resserrent()
    {
        var v2 = _song.AddVersion("v2", SongVersionStatus.Demo, null, null, Later);

        var copies = v2.CarryOverFiles(_v1, new Dictionary<Guid, string> { [_mix.Id] = "x.mp3", [_tab.Id] = "z.gp" }, Later);

        Assert.Equal(("Mix", 0), (copies[0].StemName, copies[0].StemPosition));   // la batterie n'est pas reprise
        var v3 = _song.AddVersion("v3", SongVersionStatus.Demo, null, null, Later);
        var tabOnly = v3.CarryOverFiles(_v1, new Dictionary<Guid, string> { [_drums.Id] = "y.mp3", [_tab.Id] = "t.gp" }, Later);
        Assert.Null(tabOnly[1].SyncAudioFileId);
        Assert.Null(tabOnly[1].SyncPoints);
    }

    [Fact]
    public void Refuse_un_fichier_a_la_corbeille_sans_rien_copier()
    {
        _v1.TrashFile(_pdf.Id, "user", Now);
        var v2 = _song.AddVersion("v2", SongVersionStatus.Demo, null, null, Later);

        Assert.Throws<DomainException>(() => v2.CarryOverFiles(_v1,
            new Dictionary<Guid, string> { [_mix.Id] = "x.mp3", [_pdf.Id] = "y.pdf" }, Later));
        Assert.Empty(v2.Files);
    }

    [Fact]
    public void Refuse_une_version_d_un_autre_morceau_ou_elle_meme()
    {
        var other = Song.Create(_song.BandId, "Autre", null, null, Now).AddVersion("v1", SongVersionStatus.Demo, null, null, Now);
        var keys = new Dictionary<Guid, string> { [_mix.Id] = "x.mp3" };

        Assert.Throws<DomainException>(() => other.CarryOverFiles(_v1, keys, Later));
        Assert.Throws<DomainException>(() => _v1.CarryOverFiles(_v1, keys, Later));
    }
}
