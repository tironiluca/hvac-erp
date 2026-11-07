using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HvacErp.Domain.Entities;

namespace HvacErp.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="ScheduledInstallation"/>.</summary>
public class ScheduledInstallationConfiguration : IEntityTypeConfiguration<ScheduledInstallation>
{
    public void Configure(EntityTypeBuilder<ScheduledInstallation> builder)
    {
        builder.Property(s => s.Slot).HasMaxLength(30).IsRequired();
        builder.Property(s => s.Notes).HasMaxLength(2000);
        builder.Property(s => s.UpdatedBy).HasMaxLength(256);
        builder.Property(s => s.FloorPlanSketchJson).HasColumnType("jsonb");

        // Optimistic-concurrency guard for the shared calendar: EF appends the last-known token to
        // the UPDATE's WHERE clause, so a racing save (0 rows affected) throws DbUpdateConcurrencyException.
        builder.Property(s => s.ConcurrencyToken).IsConcurrencyToken();

        builder.HasOne<DaySlot>().WithMany().HasForeignKey(s => s.Slot).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.Installers)
            .WithOne(i => i.ScheduledInstallation)
            .HasForeignKey(i => i.ScheduledInstallationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>EF Core mapping for <see cref="InstallationInstaller"/>.</summary>
public class InstallationInstallerConfiguration : IEntityTypeConfiguration<InstallationInstaller>
{
    public void Configure(EntityTypeBuilder<InstallationInstaller> builder)
    {
        builder.HasKey(i => new { i.ScheduledInstallationId, i.InstallerId });

        builder.HasOne(i => i.Installer)
            .WithMany()
            .HasForeignKey(i => i.InstallerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
