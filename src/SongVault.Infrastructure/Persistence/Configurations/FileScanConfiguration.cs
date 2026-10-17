using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SongVault.Domain.Files;

namespace SongVault.Infrastructure.Persistence.Configurations;

internal sealed class FileScanConfiguration : IEntityTypeConfiguration<FileScan>
{
    public void Configure(EntityTypeBuilder<FileScan> builder)
    {
        builder.ToTable("FileScans");
        builder.HasKey(s => s.SongFileId);
        builder.Property(s => s.SongFileId).ValueGeneratedNever();
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.Threat).HasMaxLength(FileScan.ThreatMaxLength);
        builder.Property(s => s.Error).HasMaxLength(FileScan.ErrorMaxLength);
        builder.Ignore(s => s.IsReadable);

        // Une analyse par fichier, effacée avec lui (et donc avec sa version, son morceau, son groupe)
        builder.HasOne<SongFile>().WithOne()
               .HasForeignKey<FileScan>(s => s.SongFileId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}