using SongVault.Domain.Audit;
using SongVault.Domain.Common;

namespace SongVault.Domain.Tests.Audit;

public sealed class AuditEntryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid BandId = Guid.NewGuid();

    [Fact]
    public void Record_garde_l_auteur_l_action_et_la_date()
    {
        var entry = AuditEntry.Record(BandId, "ana", AuditAction.SongDeleted, "  Nocturne ", Now);

        Assert.Equal((BandId, "ana", AuditAction.SongDeleted, "Nocturne", Now),
            (entry.BandId, entry.ActorId, entry.Action, entry.Subject, entry.OccurredAt));
        Assert.Null(entry.TargetUserId);
        Assert.Null(entry.Details);
    }

    [Fact]
    public void Sans_auteur_c_est_SongVault()
        => Assert.Null(AuditEntry.Record(BandId, null, AuditAction.FileQuarantined, "mix.wav", Now).ActorId);

    [Fact]
    public void Un_texte_trop_long_est_tronque_pas_refuse()
    {
        var entry = AuditEntry.Record(BandId, "ana", AuditAction.FileDeleted, new string('a', 500), Now,
            details: new string('b', 500));

        Assert.Equal(AuditEntry.SubjectMaxLength, entry.Subject!.Length);
        Assert.EndsWith("…", entry.Subject);
        Assert.Equal(AuditEntry.DetailsMaxLength, entry.Details!.Length);
    }

    [Fact]
    public void Record_refuse_une_ligne_sans_groupe_ou_une_action_inconnue()
    {
        Assert.Throws<DomainException>(() => AuditEntry.Record(Guid.Empty, "ana", AuditAction.SongDeleted, null, Now));
        Assert.Throws<DomainException>(() => AuditEntry.Record(BandId, "ana", (AuditAction)999, null, Now));
        Assert.Throws<DomainException>(() => AuditEntry.Record(BandId, " ", AuditAction.SongDeleted, null, Now));
    }

    [Theory]
    [InlineData("niko@exemple.fr", "n***@exemple.fr")]
    [InlineData("a@b.c", "a***@b.c")]
    [InlineData("sans-arobase", "***")]
    [InlineData("@exemple.fr", "***")]
    public void MaskEmail_ne_laisse_que_la_premiere_lettre_et_le_domaine(string email, string expected)
        => Assert.Equal(expected, AuditEntry.MaskEmail(email));

    [Fact]
    public void Aucune_propriete_ne_se_modifie_hors_de_Record()
        => Assert.All(typeof(AuditEntry).GetProperties(),
            p => Assert.False(p.SetMethod?.IsPublic ?? false, $"{p.Name} a un setter public"));
}