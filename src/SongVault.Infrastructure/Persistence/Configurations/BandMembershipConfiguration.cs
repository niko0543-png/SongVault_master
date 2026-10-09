using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SongVault.Domain.Bands;

internal sealed class BandMembershipConfiguration : IEntityTypeConfiguration<BandMembership>
{
    public void Configure(EntityTypeBuilder<BandMembership> builder)
    {
        builder.ToTable("BandMemberships");
        builder.HasKey(m => new { m.BandId, m.UserId });
        builder.Property(m => m.UserId).HasMaxLength(BandMembership.UserIdMaxLength);
        builder.Property(m => m.Role).HasConversion<string>().HasMaxLength(20);   // comme SongVersion.Status
        builder.HasIndex(m => m.UserId);                                         // « mes groupes »
        builder.HasOne<IdentityUser>().WithMany()
               .HasForeignKey(m => m.UserId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}