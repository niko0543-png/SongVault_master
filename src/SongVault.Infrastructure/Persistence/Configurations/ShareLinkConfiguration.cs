using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SongVault.Domain.Bands;
using SongVault.Domain.Releases;
using SongVault.Domain.Sharing;
using SongVault.Domain.Songs;

namespace SongVault.Infrastructure.Persistence.Configurations;

internal sealed class ShareLinkConfiguration : IEntityTypeConfiguration<ShareLink>
{
    public void Configure(EntityTypeBuilder<ShareLink> builder)
    {
        // La base refuse un lien sans cible, ou avec deux
        builder.ToTable("ShareLinks", t => t.HasCheckConstraint(
            "CK_ShareLinks_Target",
            "([TargetType] = 'Version' AND [VersionId] IS NOT NULL AND [ReleaseId] IS NULL)"
            + " OR ([TargetType] = 'Release' AND [ReleaseId] IS NOT NULL AND [VersionId] IS NULL)"));

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();                       // v7, vient du domaine
        builder.Property(l => l.TargetType).HasConversion<string>().HasMaxLength(10);
        builder.Property(l => l.Label).HasMaxLength(ShareLink.LabelMaxLength);
        builder.Property(l => l.TokenHash).HasMaxLength(ShareLink.TokenHashLength).IsFixedLength().IsRequired();
        builder.Property(l => l.PasswordHash).HasMaxLength(200);                  // format v3 d'Identity : 84 caractères
        builder.Property(l => l.AccessSalt).HasMaxLength(ShareLink.AccessSaltLength).IsFixedLength().IsRequired();
        builder.Property(l => l.CreatedById).HasMaxLength(BandMembership.UserIdMaxLength);

        builder.HasIndex(l => l.TokenHash).IsUnique();                 // ouverture par le lien

        // Groupe, version ou sortie supprimés : leurs liens partent avec eux, journal compris.
        // Un seul chemin de cascade par table : Songs et Releases sont en Restrict vers Bands.
        builder.HasOne<Band>().WithMany()
               .HasForeignKey(l => l.BandId)
               .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<SongVersion>().WithMany()
               .HasForeignKey(l => l.VersionId)
               .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Release>().WithMany()
               .HasForeignKey(l => l.ReleaseId)
               .OnDelete(DeleteBehavior.Cascade);
        // Compte purgé (#5) : le lien reste au groupe, sans auteur. Rien à ajouter dans AccountRepository.PurgeAsync.
        builder.HasOne<IdentityUser>().WithMany()
               .HasForeignKey(l => l.CreatedById)
               .OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class ShareLinkAccessConfiguration : IEntityTypeConfiguration<ShareLinkAccess>
{
    public void Configure(EntityTypeBuilder<ShareLinkAccess> builder)
    {
        builder.ToTable("ShareLinkAccesses");
        builder.HasKey(a => a.Id);                                       // bigint IDENTITY : l'ordre d'arrivée
        builder.Property(a => a.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.IpHash).HasMaxLength(ShareLinkAccess.IpHashLength).IsFixedLength();
        builder.Property(a => a.UserAgent).HasMaxLength(ShareLinkAccess.UserAgentMaxLength);

        builder.HasIndex(a => new { a.ShareLinkId, a.Id });              // journal d'un lien, le plus récent d'abord
        builder.HasOne<ShareLink>().WithMany()
               .HasForeignKey(a => a.ShareLinkId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}