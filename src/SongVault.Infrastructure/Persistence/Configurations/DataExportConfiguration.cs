using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SongVault.Domain.Accounts;
using SongVault.Domain.Bands;

internal sealed class DataExportConfiguration : IEntityTypeConfiguration<DataExport>
{
    public void Configure(EntityTypeBuilder<DataExport> builder)
    {
        builder.ToTable("DataExports");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.UserId).HasMaxLength(BandMembership.UserIdMaxLength).IsRequired();
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.StorageKey).HasMaxLength(DataExport.StorageKeyMaxLength);

        builder.HasIndex(e => new { e.UserId, e.RequestedAt });       // « mon dernier export »
        builder.HasOne<IdentityUser>().WithMany()
               .HasForeignKey(e => e.UserId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}