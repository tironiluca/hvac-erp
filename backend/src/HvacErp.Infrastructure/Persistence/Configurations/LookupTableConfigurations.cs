using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HvacErp.Domain.Entities;

namespace HvacErp.Infrastructure.Persistence.Configurations;

/// <summary>
/// The enum-replacement lookup tables (Phase 8c). Each has a string primary key <c>code</c> that
/// referencing columns store verbatim; the rows are seeded idempotently by <c>DevDataSeeder</c>.
/// </summary>
internal static class LookupTableBuilder
{
    public static void ConfigureBase<T>(EntityTypeBuilder<T> builder) where T : LookupBase
    {
        builder.HasKey(x => x.Code);
        builder.Property(x => x.Code).HasMaxLength(30);
        builder.Property(x => x.Label).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.Color).HasMaxLength(9);
    }
}

/// <summary>EF Core mapping for <see cref="VatOption"/>.</summary>
public class VatOptionConfiguration : IEntityTypeConfiguration<VatOption>
{
    public void Configure(EntityTypeBuilder<VatOption> builder)
    {
        LookupTableBuilder.ConfigureBase(builder);
        builder.Property(v => v.BlendShape).HasMaxLength(20).IsRequired();
        builder.Property(v => v.PdfLabel).HasMaxLength(100).IsRequired();
    }
}

/// <summary>EF Core mapping for <see cref="RepairStatus"/>.</summary>
public class RepairStatusConfiguration : IEntityTypeConfiguration<RepairStatus>
{
    public void Configure(EntityTypeBuilder<RepairStatus> builder)
    {
        LookupTableBuilder.ConfigureBase(builder);
        builder.HasIndex(s => s.LegacyOrdinal).IsUnique();
    }
}

/// <summary>EF Core mapping for <see cref="QuoteStatus"/>.</summary>
public class QuoteStatusConfiguration : IEntityTypeConfiguration<QuoteStatus>
{
    public void Configure(EntityTypeBuilder<QuoteStatus> builder) => LookupTableBuilder.ConfigureBase(builder);
}

/// <summary>EF Core mapping for <see cref="DuctSystemType"/>.</summary>
public class DuctSystemTypeConfiguration : IEntityTypeConfiguration<DuctSystemType>
{
    public void Configure(EntityTypeBuilder<DuctSystemType> builder) => LookupTableBuilder.ConfigureBase(builder);
}

/// <summary>EF Core mapping for <see cref="QuoteDepartment"/>.</summary>
public class QuoteDepartmentConfiguration : IEntityTypeConfiguration<QuoteDepartment>
{
    public void Configure(EntityTypeBuilder<QuoteDepartment> builder) => LookupTableBuilder.ConfigureBase(builder);
}

/// <summary>EF Core mapping for <see cref="DaySlot"/>.</summary>
public class DaySlotConfiguration : IEntityTypeConfiguration<DaySlot>
{
    public void Configure(EntityTypeBuilder<DaySlot> builder) => LookupTableBuilder.ConfigureBase(builder);
}

/// <summary>EF Core mapping for <see cref="RepairStatusEvent"/>.</summary>
public class RepairStatusEventConfiguration : IEntityTypeConfiguration<RepairStatusEvent>
{
    public void Configure(EntityTypeBuilder<RepairStatusEvent> builder)
    {
        builder.Property(e => e.Status).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(2000);
        builder.Property(e => e.ChangedBy).HasMaxLength(200);

        builder.HasIndex(e => new { e.RepairId, e.OccurredAt });

        builder.HasOne(e => e.Repair)
            .WithMany(r => r.StatusEvents)
            .HasForeignKey(e => e.RepairId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<RepairStatus>()
            .WithMany()
            .HasForeignKey(e => e.Status)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
