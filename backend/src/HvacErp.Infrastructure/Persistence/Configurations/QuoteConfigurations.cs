using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HvacErp.Domain.Entities;

namespace HvacErp.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="Quote"/>.</summary>
public class QuoteConfiguration : IEntityTypeConfiguration<Quote>
{
    public void Configure(EntityTypeBuilder<Quote> builder)
    {
        builder.HasIndex(q => new { q.Number, q.Version, q.Revision }).IsUnique();

        builder.Property(q => q.Department).HasMaxLength(30).IsRequired();
        builder.Property(q => q.Vat).HasMaxLength(30).IsRequired();
        builder.Property(q => q.CurrentStatus).HasMaxLength(30).IsRequired();
        builder.Property(q => q.FloorPlanSketchJson).HasColumnType("jsonb");

        builder.HasOne<QuoteDepartment>().WithMany().HasForeignKey(q => q.Department).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<VatOption>().WithMany().HasForeignKey(q => q.Vat).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<QuoteStatus>().WithMany().HasForeignKey(q => q.CurrentStatus).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(q => q.Client)
            .WithMany(c => c.Quotes)
            .HasForeignKey(q => q.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(q => q.Operator)
            .WithMany()
            .HasForeignKey(q => q.OperatorId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(q => q.ParentQuote)
            .WithMany()
            .HasForeignKey(q => q.ParentQuoteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(q => q.Products)
            .WithOne(p => p.Quote)
            .HasForeignKey(p => p.QuoteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(q => q.DuctedUnits)
            .WithOne(d => d.Quote)
            .HasForeignKey(d => d.QuoteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(q => q.ServiceItems)
            .WithOne(s => s.Quote)
            .HasForeignKey(s => s.QuoteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(q => q.Extras)
            .WithOne(e => e.Quote)
            .HasForeignKey(e => e.QuoteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(q => q.StatusEvents)
            .WithOne(e => e.Quote)
            .HasForeignKey(e => e.QuoteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(q => q.Attachments)
            .WithOne(a => a.Quote)
            .HasForeignKey(a => a.QuoteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(q => q.Repairs)
            .WithOne(r => r.Quote)
            .HasForeignKey(r => r.QuoteId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(q => q.ScheduledInstallations)
            .WithOne(s => s.Quote)
            .HasForeignKey(s => s.QuoteId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>EF Core mapping for <see cref="QuoteProduct"/>.</summary>
public class QuoteProductConfiguration : IEntityTypeConfiguration<QuoteProduct>
{
    public void Configure(EntityTypeBuilder<QuoteProduct> builder)
    {
        builder.Property(p => p.Role).HasConversion<string>().HasMaxLength(20);

        builder.ToTable(t => t.HasCheckConstraint("ck_quote_products_quantity_positive", "quantity >= 1"));

        builder.HasOne(p => p.Product)
            .WithMany(pr => pr.QuoteProducts)
            .HasForeignKey(p => p.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>EF Core mapping for <see cref="DuctedUnit"/>.</summary>
public class DuctedUnitConfiguration : IEntityTypeConfiguration<DuctedUnit>
{
    public void Configure(EntityTypeBuilder<DuctedUnit> builder)
    {
        builder.HasMany(d => d.Outlets)
            .WithOne(o => o.DuctedUnit)
            .HasForeignKey(o => o.DuctedUnitId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>EF Core mapping for <see cref="DuctedUnitOutlet"/>.</summary>
public class DuctedUnitOutletConfiguration : IEntityTypeConfiguration<DuctedUnitOutlet>
{
    public void Configure(EntityTypeBuilder<DuctedUnitOutlet> builder)
    {
        builder.Property(o => o.SystemType).HasMaxLength(30).IsRequired();
        builder.HasOne<DuctSystemType>().WithMany().HasForeignKey(o => o.SystemType).OnDelete(DeleteBehavior.Restrict);

        // One outlet per position within a ducted system; positions are 1-based.
        builder.HasIndex(o => new { o.DuctedUnitId, o.Position }).IsUnique();
        builder.ToTable(t => t.HasCheckConstraint("ck_ducted_unit_outlets_position_positive", "position >= 1"));
    }
}

/// <summary>EF Core mapping for <see cref="QuoteServiceItem"/>.</summary>
public class QuoteServiceItemConfiguration : IEntityTypeConfiguration<QuoteServiceItem>
{
    public void Configure(EntityTypeBuilder<QuoteServiceItem> builder)
    {
        builder.HasOne(s => s.ServiceItem)
            .WithMany()
            .HasForeignKey(s => s.ServiceItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>EF Core mapping for <see cref="QuoteExtra"/>.</summary>
public class QuoteExtraConfiguration : IEntityTypeConfiguration<QuoteExtra>
{
    public void Configure(EntityTypeBuilder<QuoteExtra> builder)
    {
        // Each extra type appears at most once per quote — it's one line with a quantity,
        // never two rows for the same kind of work.
        builder.HasIndex(e => new { e.QuoteId, e.ExtraTypeId }).IsUnique();
        builder.ToTable(t => t.HasCheckConstraint("ck_quote_extras_quantity_non_negative", "quantity >= 0"));

        builder.HasOne(e => e.ExtraType)
            .WithMany()
            .HasForeignKey(e => e.ExtraTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>EF Core mapping for <see cref="QuoteStatusEvent"/>.</summary>
public class QuoteStatusEventConfiguration : IEntityTypeConfiguration<QuoteStatusEvent>
{
    public void Configure(EntityTypeBuilder<QuoteStatusEvent> builder)
    {
        builder.Property(e => e.Status).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(2000);
        builder.Property(e => e.ChangedBy).HasMaxLength(200);

        builder.HasIndex(e => new { e.QuoteId, e.OccurredAt });

        builder.HasOne<QuoteStatus>().WithMany().HasForeignKey(e => e.Status).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>EF Core mapping for <see cref="QuoteAttachment"/>.</summary>
public class QuoteAttachmentConfiguration : IEntityTypeConfiguration<QuoteAttachment>
{
    public void Configure(EntityTypeBuilder<QuoteAttachment> builder)
    {
        builder.Property(a => a.FileName).HasMaxLength(255).IsRequired();
        builder.Property(a => a.FileUrl).HasMaxLength(1000).IsRequired();
    }
}
