using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SongVault.Domain.Files;

namespace SongVault.Infrastructure.Persistence.Configurations;

internal sealed class AudioPreviewConfiguration : IEntityTypeConfiguration<AudioPreview>
{
    public void Configure(EntityTypeBuilder<AudioPreview> builder)
    {
        builder.ToTable("AudioPreviews");
        builder.HasKey(p => p.SongFileId);
        builder.Property(p => p.SongFileId).ValueGeneratedNever();
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.PreviewStorageKey).HasMaxLength(100);
        builder.Property(p => p.Peaks).HasMaxLength(AudioPreview.PeakCount);           // varbinary(1000)
        builder.Property(p => p.Error).HasMaxLength(AudioPreview.ErrorMaxLength);

        // Un aperçu par fichier, effacé avec lui (et donc avec sa version, son morceau, son groupe)
        builder.HasOne<SongFile>().WithOne()
               .HasForeignKey<AudioPreview>(p => p.SongFileId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}