using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SongVault.Domain.Bands;
using SongVault.Domain.Invitations;

internal sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("Invitations");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();                       // v7, vient du domaine
        builder.Property(i => i.Email).HasMaxLength(Invitation.EmailMaxLength).IsRequired();
        builder.Property(i => i.Role).HasConversion<string>().HasMaxLength(20);   // comme BandMemberships.Role
        builder.Property(i => i.TokenHash).HasMaxLength(Invitation.TokenHashLength).IsFixedLength().IsRequired();
        builder.Property(i => i.InvitedById).HasMaxLength(BandMembership.UserIdMaxLength).IsRequired();
        builder.Property(i => i.AcceptedById).HasMaxLength(BandMembership.UserIdMaxLength);

        // Deux acceptations simultanées : la seconde échoue (DbUpdateConcurrencyException → 409)
        builder.Property<byte[]>("RowVersion").IsRowVersion();

        builder.HasIndex(i => i.TokenHash).IsUnique();                 // recherche par lien
        builder.HasIndex(i => new { i.BandId, i.Email });               // invitations en attente d'une adresse
        builder.HasOne<Band>().WithMany()
               .HasForeignKey(i => i.BandId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}