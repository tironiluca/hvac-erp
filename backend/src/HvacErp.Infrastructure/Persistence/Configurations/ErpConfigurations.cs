using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HvacErp.Domain.Entities;
using HvacErp.Infrastructure.Erp;

namespace HvacErp.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="ErpOrderExport"/>.</summary>
public class ErpOrderExportConfiguration : IEntityTypeConfiguration<ErpOrderExport>
{
    public void Configure(EntityTypeBuilder<ErpOrderExport> builder)
    {
        // One export per quote: re-sending reuses the row.
        builder.HasIndex(e => e.QuoteId).IsUnique();
        // The worker's query: due pending rows.
        builder.HasIndex(e => new { e.Status, e.NextAttemptAt });

        builder.Property(e => e.Connector).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(20).IsRequired();
        builder.Property(e => e.ErpOrderNumber).HasMaxLength(100);
        builder.Property(e => e.LastError).HasMaxLength(2000);
        builder.Property(e => e.PayloadJson).HasColumnType("jsonb").IsRequired();
        builder.ToTable(t => t.HasCheckConstraint("ck_erp_order_exports_status", "status IN ('Pending', 'Sent', 'Failed')"));

        builder.HasOne(e => e.Quote)
            .WithMany()
            .HasForeignKey(e => e.QuoteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>EF Core mapping for the mock ERP's <see cref="MockErpOrder"/>.</summary>
public class MockErpOrderConfiguration : IEntityTypeConfiguration<MockErpOrder>
{
    public void Configure(EntityTypeBuilder<MockErpOrder> builder)
    {
        builder.HasIndex(o => o.ExternalReference).IsUnique();
        builder.Property(o => o.OrderNumber).HasMaxLength(30).IsRequired();
        builder.Property(o => o.ExternalReference).HasMaxLength(100).IsRequired();
        builder.Property(o => o.CustomerName).HasMaxLength(200).IsRequired();
        builder.Property(o => o.PayloadJson).HasColumnType("jsonb").IsRequired();
    }
}
