using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SongVault.Domain.Songs;

namespace SongVault.Infrastructure.Persistence.Configurations;

/// <summary>Les crédits d'un morceau (#22, ADR 0029) : supprimés avec lui, par la base.</summary>
internal sealed class CreditConfiguration : IEntityTypeConfiguration<Credit>
{
    public void Configure(EntityTypeBuilder<Credit> builder)
    {
        builder.ToTable("Credits", t => t.HasCheckConstraint("CK_Credits_SharePercent",
            "[SharePercent] > 0 AND [SharePercent] <= 100"));
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Name).HasMaxLength(Credit.NameMaxLength).IsRequired();
        builder.Property(c => c.Role).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.SharePercent).HasPrecision(5, 2);
        builder.HasIndex(c => new { c.SongId, c.Position });
    }
}