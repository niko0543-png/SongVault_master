using SongVault.Domain.Common;
using SongVault.Domain.Files;

namespace SongVault.Domain.Tests.Files;

public sealed class AudioSplitTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 20, 0, 0, TimeSpan.Zero);
    private const int Duration = 3_600_000;                                 // une heure de répétition

    private static AudioSplit Request(params AudioSplitPart[] parts)
        => AudioSplit.Request(Guid.NewGuid(), Guid.NewGuid(), "repet-24-10.wav", "member-1", Duration, parts, Now);

    [Fact]
    public void Les_passages_sont_ranges_dans_l_ordre_et_leurs_titres_nettoyes()
    {
        var song = Guid.NewGuid();
        var split = Request(new AudioSplitPart(600_000, 900_000, "  Nocturne ", song), new(0, 240_000, "Intro", null));

        Assert.Equal(AudioSplitStatus.Pending, split.Status);
        Assert.Equal(["Intro", "Nocturne"], split.Parts.Select(p => p.Title));
        Assert.Equal(song, split.Parts[1].SongId);
        Assert.Null(split.Parts[0].SongId);
    }

    [Theory]
    [InlineData(-1, 10_000)]                     // avant le début
    [InlineData(10_000, Duration + 1)]           // après la fin
    [InlineData(10_000, 10_500)]                 // moins d'une seconde
    public void Refuse_un_passage_hors_du_fichier_ou_trop_court(int start, int end)
        => Assert.Throws<DomainException>(() => Request(new AudioSplitPart(start, end, "Nocturne", null)));

    [Fact]
    public void Refuse_deux_passages_qui_se_chevauchent()
        => Assert.Throws<DomainException>(() => Request(new AudioSplitPart(0, 60_000, "A", null), new(59_000, 120_000, "B", null)));

    [Fact]
    public void Deux_passages_bout_a_bout_et_un_trou_entre_eux_sont_acceptes()
    {
        var split = Request(new AudioSplitPart(0, 60_000, "A", null), new(60_000, 120_000, "B", null), new(300_000, 400_000, "C", null));

        Assert.Equal(3, split.Parts.Count);
    }

    [Fact]
    public void Refuse_aucun_passage_ou_trop_de_passages()
    {
        Assert.Throws<DomainException>(() => Request());
        var many = Enumerable.Range(0, AudioSplit.MaxParts + 1)
            .Select(i => new AudioSplitPart(i * 10_000, i * 10_000 + 5_000, $"P{i}", null)).ToArray();
        Assert.Throws<DomainException>(() => Request(many));
    }

    [Fact]
    public void Refuse_un_passage_sans_titre()
        => Assert.Throws<DomainException>(() => Request(new AudioSplitPart(0, 60_000, "  ", null)));

    [Fact]
    public void Refuse_tant_que_la_duree_n_est_pas_connue()
        => Assert.Throws<DomainException>(() =>
            AudioSplit.Request(Guid.NewGuid(), Guid.NewGuid(), "repet.wav", "member-1", 0, [new(0, 60_000, "A", null)], Now));

    [Fact]
    public void Fait_il_garde_ou_chaque_passage_est_arrive()
    {
        var split = Request(new AudioSplitPart(0, 60_000, "A", null), new(60_000, 120_000, "B", null));
        var created = new[] { (Guid.NewGuid(), Guid.NewGuid()), (Guid.NewGuid(), Guid.NewGuid()) };

        split.MarkDone(created, Now.AddMinutes(2));

        Assert.Equal(AudioSplitStatus.Done, split.Status);
        Assert.Equal(created[1].Item2, split.Parts[1].CreatedVersionId);
        Assert.Equal(Now.AddMinutes(2), split.CompletedAt);
        split.MarkFailed("trop tard", Now);                              // sans effet une fois fait
        Assert.Equal(AudioSplitStatus.Done, split.Status);
    }

    [Fact]
    public void Nomme_le_fichier_la_version_et_les_notes_d_apres_l_original()
    {
        var split = Request(new AudioSplitPart(3_500_000, 3_600_000, "Nocturne", null));
        var part = split.Parts[0];

        Assert.Equal("Nocturne (repet-24-10).wav", split.PartFileName(part, ".wav"));
        Assert.Equal("Extrait de repet-24-10", split.PartVersionTitle());
        Assert.Equal("Découpé dans « repet-24-10.wav », de 58:20 à 1:00:00.", split.PartNotes(part));
    }

    [Fact]
    public void Un_nom_de_fichier_trop_long_est_raccourci_en_gardant_l_extension()
    {
        var split = AudioSplit.Request(Guid.NewGuid(), Guid.NewGuid(), new string('r', 100) + ".wav", "member-1", Duration,
            [new(0, 60_000, new string('a', 200), null)], Now);

        var name = split.PartFileName(split.Parts[0], ".mp3");

        Assert.Equal(SongFile.OriginalFileNameMaxLength, name.Length);
        Assert.EndsWith(".mp3", name);
    }
}
