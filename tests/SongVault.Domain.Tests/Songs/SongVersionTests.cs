using SongVault.Domain.Common;
using SongVault.Domain.Songs;

namespace SongVault.Domain.Tests.Songs;

public sealed class SongVersionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AddVersion_numerote_1_2_3()
    {
        var song = Song.Create("owner-1", "Nocturne", null, null, Now);

        var numbers = new[] { "Idée", "Maquette", "Studio" }
            .Select(t => song.AddVersion(t, SongVersionStatus.Demo, null, null, Now).Number)
            .ToList();

        Assert.Equal([1, 2, 3], numbers);
        Assert.Equal(3, song.LastVersionNumber);
        Assert.All(song.Versions, v => Assert.Equal(song.Id, v.SongId));
    }

    [Fact]
    public void AddVersion_titre_invalide_n_incremente_pas_le_compteur()
    {
        var song = Song.Create("owner-1", "Nocturne", null, null, Now);
        song.AddVersion("v1", SongVersionStatus.Idea, null, null, Now);

        Assert.Throws<DomainException>(() => song.AddVersion(" ", SongVersionStatus.Demo, null, null, Now));

        Assert.Equal(1, song.LastVersionNumber);
        Assert.Single(song.Versions);
        Assert.Equal(2, song.AddVersion("v2", SongVersionStatus.Demo, null, null, Now).Number); // pas de trou
    }

    [Fact]
    public void AddVersion_statut_inconnu_est_refuse()
    {
        var song = Song.Create("owner-1", "Nocturne", null, null, Now);

        Assert.Throws<DomainException>(() => song.AddVersion("v1", (SongVersionStatus)99, null, null, Now));
        Assert.Equal(0, song.LastVersionNumber);
    }

    [Fact]
    public void Update_modifie_la_version_sans_changer_son_numero()
    {
        var song = Song.Create("owner-1", "Nocturne", null, null, Now);
        var version = song.AddVersion("v1", SongVersionStatus.Idea, null, null, Now);

        version.Update("v1 retravaillée", SongVersionStatus.Rehearsal, "tempo plus lent", null, Now.AddDays(1));

        Assert.Equal(1, version.Number);
        Assert.Equal(SongVersionStatus.Rehearsal, version.Status);
        Assert.Equal(Now.AddDays(1), version.UpdatedAt);
    }
}