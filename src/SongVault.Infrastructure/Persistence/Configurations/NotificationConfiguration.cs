using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SongVault.Domain.Activity;
using SongVault.Domain.Bands;
using SongVault.Domain.Notifications;

namespace SongVault.Infrastructure.Persistence.Configurations;

internal sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("NotificationPreferences");
        builder.HasKey(p => new { p.UserId, p.Kind });
        builder.Property(p => p.UserId).HasMaxLength(BandMembership.UserIdMaxLength);
        builder.Property(p => p.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Mode).HasConversion<string>().HasMaxLength(20);

        // Compte effacé : ses préférences partent avec lui
        builder.HasOne<IdentityUser>().WithMany()
               .HasForeignKey(p => p.UserId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class NotificationDigestItemConfiguration : IEntityTypeConfiguration<NotificationDigestItem>
{
    public void Configure(EntityTypeBuilder<NotificationDigestItem> builder)
    {
        builder.ToTable("NotificationDigestItems");
        builder.HasKey(i => i.Id);                                             // bigint IDENTITY
        builder.Property(i => i.UserId).HasMaxLength(BandMembership.UserIdMaxLength);
        builder.Property(i => i.Kind).HasConversion<string>().HasMaxLength(20);

        // Compte effacé, ou événement parti avec son groupe : l'élément part aussi
        builder.HasOne<IdentityUser>().WithMany()
               .HasForeignKey(i => i.UserId)
               .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ActivityEvent>().WithMany()
               .HasForeignKey(i => i.EventId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => new { i.UserId, i.Id });                         // le résumé d'un compte
    }
}