using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SongVault.Domain.Bands;

internal sealed class BandConfiguration : IEntityTypeConfiguration<Band>
{
    public void Configure(EntityTypeBuilder<Band> builder)
    {
        builder.ToTable("Bands");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();   // l'Id vient du domaine (v7)
        builder.Property(b => b.Name).HasMaxLength(Band.NameMaxLength).IsRequired();
        builder.HasMany(b => b.Memberships)
               .WithOne()
               .HasForeignKey(m => m.BandId)
               .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(b => b.Memberships).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}