using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HvacErp.Domain.Entities;

namespace HvacErp.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="AppUser"/>.</summary>
public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.HasIndex(u => u.Email).IsUnique();
        builder.Property(u => u.Email).HasMaxLength(200);
        builder.Property(u => u.DisplayName).HasMaxLength(200);
        builder.Property(u => u.IsActive).HasDefaultValue(true);
    }
}

/// <summary>EF Core mapping for <see cref="Installer"/>.</summary>
public class InstallerConfiguration : IEntityTypeConfiguration<Installer>
{
    public void Configure(EntityTypeBuilder<Installer> builder)
    {
        builder.Property(i => i.Name).HasMaxLength(100).IsRequired();
        builder.Property(i => i.Mobile).HasMaxLength(30);
        builder.Property(i => i.Email).HasMaxLength(150);
        builder.Property(i => i.Department).HasMaxLength(100);
    }
}

/// <summary>EF Core mapping for <see cref="Operator"/>.</summary>
public class OperatorConfiguration : IEntityTypeConfiguration<Operator>
{
    public void Configure(EntityTypeBuilder<Operator> builder)
    {
        builder.Property(o => o.Code).HasMaxLength(50).IsRequired();
        builder.Property(o => o.Name).HasMaxLength(100).IsRequired();
        builder.Property(o => o.Email).HasMaxLength(150);
    }
}

/// <summary>EF Core mapping for <see cref="ClientSource"/>.</summary>
public class ClientSourceConfiguration : IEntityTypeConfiguration<ClientSource>
{
    public void Configure(EntityTypeBuilder<ClientSource> builder)
    {
        builder.Property(s => s.Description).HasMaxLength(150).IsRequired();
    }
}

/// <summary>EF Core mapping for <see cref="ProductType"/>.</summary>
public class ProductTypeConfiguration : IEntityTypeConfiguration<ProductType>
{
    public void Configure(EntityTypeBuilder<ProductType> builder)
    {
        builder.Property(t => t.Name).HasMaxLength(100).IsRequired();
    }
}

/// <summary>EF Core mapping for <see cref="ProductPriceList"/>.</summary>
public class ProductPriceListConfiguration : IEntityTypeConfiguration<ProductPriceList>
{
    public void Configure(EntityTypeBuilder<ProductPriceList> builder)
    {
        builder.Property(p => p.Description).HasMaxLength(200).IsRequired();

        builder.HasMany(p => p.DiscountTiers)
            .WithOne(t => t.PriceList)
            .HasForeignKey(t => t.PriceListId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>EF Core mapping for <see cref="PriceListDiscountTier"/>.</summary>
public class PriceListDiscountTierConfiguration : IEntityTypeConfiguration<PriceListDiscountTier>
{
    public void Configure(EntityTypeBuilder<PriceListDiscountTier> builder)
    {
        builder.HasIndex(t => new { t.PriceListId, t.TierOrder }).IsUnique();
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_price_list_discount_tiers_percentage_range", "percentage >= 0 AND percentage <= 100"));
    }
}

/// <summary>EF Core mapping for <see cref="ExtraType"/>.</summary>
public class ExtraTypeConfiguration : IEntityTypeConfiguration<ExtraType>
{
    public void Configure(EntityTypeBuilder<ExtraType> builder)
    {
        builder.HasIndex(e => new { e.TenantId, e.Code }).IsUnique();
        builder.Property(e => e.Code).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Label).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(500);
    }
}

/// <summary>EF Core mapping for <see cref="ServiceItem"/>.</summary>
public class ServiceItemConfiguration : IEntityTypeConfiguration<ServiceItem>
{
    public void Configure(EntityTypeBuilder<ServiceItem> builder)
    {
        builder.Property(s => s.Name).HasMaxLength(150).IsRequired();
        builder.Property(s => s.Code).HasMaxLength(50);
        builder.HasIndex(s => new { s.TenantId, s.Code }).IsUnique();

        builder.HasOne(s => s.ProductType)
            .WithMany()
            .HasForeignKey(s => s.ProductTypeId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

/// <summary>EF Core mapping for <see cref="Product"/>.</summary>
public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.Property(p => p.Brand).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Model).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Ean).HasMaxLength(13);
        builder.Property(p => p.RefrigerantGas).HasMaxLength(20);

        builder.HasOne(p => p.PriceList)
            .WithMany(pl => pl.Products)
            .HasForeignKey(p => p.PriceListId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>EF Core mapping for <see cref="PortalBranding"/>.</summary>
public class PortalBrandingConfiguration : IEntityTypeConfiguration<PortalBranding>
{
    public void Configure(EntityTypeBuilder<PortalBranding> builder)
    {
        // Single-row table: the id is a fixed sentinel (1), not generated.
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();

        builder.Property(b => b.PrimarySeed).HasMaxLength(9).IsRequired();
        builder.Property(b => b.TertiarySeed).HasMaxLength(9).IsRequired();
        builder.Property(b => b.NeutralSeed).HasMaxLength(9).IsRequired();
        builder.Property(b => b.PortalTitle).HasMaxLength(200).IsRequired();
        builder.Property(b => b.LogoUrl).HasMaxLength(500);
        builder.Property(b => b.LogoDarkUrl).HasMaxLength(500);
        builder.Property(b => b.FaviconUrl).HasMaxLength(500);
        builder.Property(b => b.LoginBackgroundUrl).HasMaxLength(500);
        builder.Property(b => b.UpdatedBy).HasMaxLength(200);
    }
}

/// <summary>EF Core mapping for <see cref="PdfTemplate"/>.</summary>
public class PdfTemplateConfiguration : IEntityTypeConfiguration<PdfTemplate>
{
    public void Configure(EntityTypeBuilder<PdfTemplate> builder)
    {
        builder.Property(t => t.DocumentType).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Name).HasMaxLength(120).IsRequired();
        builder.Property(t => t.DefinitionJson).HasColumnType("jsonb").IsRequired();
        builder.Property(t => t.UpdatedBy).HasMaxLength(200);

        // One template per document type carries the layout the generator renders against.
        builder.HasIndex(t => new { t.TenantId, t.DocumentType })
            .IsUnique()
            .HasFilter("is_active");

        builder.HasIndex(t => new { t.TenantId, t.DocumentType, t.Name }).IsUnique();
    }
}
