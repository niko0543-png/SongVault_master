using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SongVault.Domain.Accounts;
using SongVault.Domain.Audit;
using SongVault.Domain.Bands;

namespace SongVault.Infrastructure.Persistence.Configurations;

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    /// <summary>Déclencheur créé par la migration AddAuditLog : il refuse toute modification (ADR 0026).</summary>
    public const string ReadOnlyTrigger = "TR_AuditEntries_ReadOnly";

    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        // Le déclencheur déclaré, EF n'écrit plus avec OUTPUT seul, que SQL Server refuse sur une table qui en a un
        builder.ToTable("AuditEntries", t => t.HasTrigger(ReadOnlyTrigger));
        builder.HasKey(e => e.Id);                                             // bigint IDENTITY
        builder.Property(e => e.ActorId).HasMaxLength(AuditEntry.UserIdMaxLength);
        builder.Property(e => e.TargetUserId).HasMaxLength(AuditEntry.UserIdMaxLength);
        builder.Property(e => e.Action).HasConversion<string>().HasMaxLength(40);
        builder.Property(e => e.Subject).HasMaxLength(AuditEntry.SubjectMaxLength);
        builder.Property(e => e.Details).HasMaxLength(AuditEntry.DetailsMaxLength);

        // Groupe effacé (purge d'un compte) : son journal part avec lui, seul cas que le déclencheur laisse passer.
        // Pas de clé étrangère vers les comptes : « compte supprimé » reste lisible dans le journal.
        builder.HasOne<Band>().WithMany()
               .HasForeignKey(e => e.BandId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.BandId, e.OccurredAt, e.Id });          // le journal, page par page
        builder.HasIndex(e => e.OccurredAt);                                    // la rétention d'un an
    }
}

internal sealed class AccountEventConfiguration : IEntityTypeConfiguration<AccountEvent>
{
    public void Configure(EntityTypeBuilder<AccountEvent> builder)
    {
        builder.ToTable("AccountEvents");
        builder.HasKey(e => e.Id);                                             // bigint IDENTITY
        builder.Property(e => e.UserId).HasMaxLength(AccountEvent.UserIdMaxLength);
        builder.Property(e => e.Type).HasConversion<string>().HasMaxLength(40);
        builder.Property(e => e.Network).HasMaxLength(AccountEvent.NetworkMaxLength);
        builder.Property(e => e.Device).HasMaxLength(AccountEvent.DeviceMaxLength);

        // Compte effacé : son historique part avec lui
        builder.HasOne<IdentityUser>().WithMany()
               .HasForeignKey(e => e.UserId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.UserId, e.OccurredAt });
        builder.HasIndex(e => e.OccurredAt);                                    // la rétention de 90 jours
    }
}