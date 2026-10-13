using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SongVault.Infrastructure.Jobs;

internal sealed class BackgroundJobConfiguration : IEntityTypeConfiguration<BackgroundJob>
{
    public void Configure(EntityTypeBuilder<BackgroundJob> builder)
    {
        builder.ToTable("BackgroundJobs");
        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id).ValueGeneratedNever();
        builder.Property(j => j.Type).HasMaxLength(BackgroundJob.TypeMaxLength).IsRequired();
        builder.Property(j => j.Payload).IsRequired();
        builder.Property(j => j.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(j => j.Error).HasMaxLength(BackgroundJob.ErrorMaxLength);

        builder.HasIndex(j => new { j.Status, j.RunAfter });          // « la prochaine tâche à faire »
    }
}