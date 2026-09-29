using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SongVault.Domain.Files;

namespace SongVault.Infrastructure.Persistence.Configurations;

internal sealed class SongFileConfiguration : IEntityTypeConfiguration<SongFile>
{
    public void Configure(EntityTypeBuilder<SongFile> builder)
    {
        builder.ToTable("SongFiles");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();
        builder.Property(f => f.OriginalFileName).HasMaxLength(SongFile.OriginalFileNameMaxLength).IsRequired();
        builder.Property(f => f.StorageKey).HasMaxLength(100).IsRequired();
        builder.HasIndex(f => f.StorageKey).IsUnique();
        builder.Property(f => f.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(f => f.FileType).HasConversion<string>().HasMaxLength(20);
    }
}