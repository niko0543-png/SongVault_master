using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SongVault.Domain.Bands;
using SongVault.Domain.Setlists;
using SongVault.Domain.Songs;

namespace SongVault.Infrastructure.Persistence.Configurations;

internal sealed class SetlistConfiguration : IEntityTypeConfiguration<Setlist>
{
    public void Configure(EntityTypeBuilder<Setlist> builder)
    {
        builder.ToTable("Setlists");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();                    // v7, vient du domaine
        builder.Property(s => s.Name).HasMaxLength(Setlist.NameMaxLength).IsRequired();
        builder.Property(s => s.Venue).HasMaxLength(Setlist.VenueMaxLength);
        builder.HasIndex(s => new { s.BandId, s.Date });
        builder.HasOne<Band>().WithMany()
               .HasForeignKey(s => s.BandId)
               .OnDelete(DeleteBehavior.Restrict);   // comme Songs et Releases : la purge d'un compte les efface d'abord

        builder.HasMany(s => s.Items)
               .WithOne()
               .HasForeignKey(i => i.SetlistId)
               .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Property<byte[]>("RowVersion").IsRowVersion();
    }
}

internal sealed class SetlistItemConfiguration : IEntityTypeConfiguration<SetlistItem>
{
    public void Configure(EntityTypeBuilder<SetlistItem> builder)
    {
        builder.ToTable("SetlistItems");
        builder.HasKey(i => new { i.SetlistId, i.SongId });            // un morceau au plus une fois par setlist
        builder.HasIndex(i => new { i.SetlistId, i.Position }).IsUnique();
        builder.HasIndex(i => i.SongId);                                 // « dans quelles setlists est ce morceau ? »
        builder.HasIndex(i => i.VersionId);
        builder.Property(i => i.Notes).HasMaxLength(SetlistItem.NotesMaxLength);
        builder.Property(i => i.CommentBefore).HasMaxLength(SetlistItem.CommentMaxLength);

        // Restrict, comme les pistes d'une sortie : SQL Server refuse deux chemins de cascade, et un morceau
        // d'une setlist ne part pas à la corbeille en silence (DeleteSongHandler refuse avec un 409).
        builder.HasOne<Song>().WithMany()
               .HasForeignKey(i => i.SongId)
               .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SongVersion>().WithMany()
               .HasForeignKey(i => i.VersionId)
               .IsRequired(false)
               .OnDelete(DeleteBehavior.Restrict);
    }
}