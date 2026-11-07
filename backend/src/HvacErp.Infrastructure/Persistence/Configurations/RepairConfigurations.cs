using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HvacErp.Domain.Entities;

namespace HvacErp.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="Repair"/>.</summary>
public class RepairConfiguration : IEntityTypeConfiguration<Repair>
{
    public void Configure(EntityTypeBuilder<Repair> builder)
    {
        builder.Property(r => r.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Status).HasMaxLength(30).IsRequired();
        builder.Property(r => r.Brand).HasMaxLength(50);
        builder.Property(r => r.FloorPlanSketchJson).HasColumnType("jsonb");

        builder.HasOne<RepairStatus>().WithMany().HasForeignKey(r => r.Status).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Client)
            .WithMany(c => c.Repairs)
            .HasForeignKey(r => r.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.ServiceItems)
            .WithOne(s => s.Repair)
            .HasForeignKey(s => s.RepairId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(r => r.ScheduleEntries)
            .WithOne(s => s.Repair)
            .HasForeignKey(s => s.RepairId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>EF Core mapping for <see cref="RepairServiceItem"/>.</summary>
public class RepairServiceItemConfiguration : IEntityTypeConfiguration<RepairServiceItem>
{
    public void Configure(EntityTypeBuilder<RepairServiceItem> builder)
    {
        builder.HasOne(s => s.ServiceItem)
            .WithMany()
            .HasForeignKey(s => s.ServiceItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>EF Core mapping for <see cref="RepairScheduleEntry"/>.</summary>
public class RepairScheduleEntryConfiguration : IEntityTypeConfiguration<RepairScheduleEntry>
{
    public void Configure(EntityTypeBuilder<RepairScheduleEntry> builder)
    {
        builder.Property(s => s.Notes).HasMaxLength(1000);

        builder.HasOne(s => s.Installer)
            .WithMany()
            .HasForeignKey(s => s.InstallerId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

/// <summary>EF Core mapping for <see cref="FGasRecord"/>.</summary>
public class FGasRecordConfiguration : IEntityTypeConfiguration<FGasRecord>
{
    public void Configure(EntityTypeBuilder<FGasRecord> builder)
    {
        builder.Property(f => f.GasType).HasMaxLength(20).IsRequired();

        // Every record hangs off exactly one parent — a quote or a repair, never both, never neither
        // (FGasRecordService.CreateAsync already enforces this; the constraint is the DB-level backstop).
        // The FKs cascade rather than SetNull: an F-Gas record has no meaning without its parent — the
        // app only ever queries it by quote_id / repair_id — so nulling the FK on parent delete would
        // just leave an unreachable row that also breaks the check above.
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_fgas_records_single_parent", "(quote_id IS NULL) <> (repair_id IS NULL)"));

        builder.HasOne(f => f.Quote)
            .WithMany(q => q.FGasRecords)
            .HasForeignKey(f => f.QuoteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.Repair)
            .WithMany(r => r.FGasRecords)
            .HasForeignKey(f => f.RepairId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
