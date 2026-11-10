using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HvacErp.Domain.Entities;

namespace HvacErp.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="Tenant"/>.</summary>
public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.Property(t => t.Name).HasMaxLength(150).IsRequired();
        builder.Property(t => t.LegalName).HasMaxLength(200);
        builder.Property(t => t.VatNumber).HasMaxLength(30);
    }
}

/// <summary>EF Core mapping for <see cref="Store"/>.</summary>
public class StoreConfiguration : IEntityTypeConfiguration<Store>
{
    public void Configure(EntityTypeBuilder<Store> builder)
    {
        builder.Property(s => s.Name).HasMaxLength(150).IsRequired();
        builder.Property(s => s.Address).HasMaxLength(250);
        builder.Property(s => s.City).HasMaxLength(100);
        builder.Property(s => s.Phone).HasMaxLength(30);
        builder.Property(s => s.Email).HasMaxLength(150);
        builder.HasIndex(s => new { s.TenantId, s.Name }).IsUnique();

        builder.HasOne(s => s.Tenant)
            .WithMany(t => t.Stores)
            .HasForeignKey(s => s.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>EF Core mapping for <see cref="TenantMembership"/>.</summary>
public class TenantMembershipConfiguration : IEntityTypeConfiguration<TenantMembership>
{
    public void Configure(EntityTypeBuilder<TenantMembership> builder)
    {
        builder.Property(m => m.Role).HasConversion<string>().HasMaxLength(20);

        // One role per person per company.
        builder.HasIndex(m => new { m.TenantId, m.UserId }).IsUnique();
        builder.HasIndex(m => m.UserId);
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_tenant_memberships_role", "role IN ('Admin', 'Sales', 'Warehouse')"));

        builder.HasOne(m => m.Tenant)
            .WithMany(t => t.Memberships)
            .HasForeignKey(m => m.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.User)
            .WithMany(u => u.Memberships)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Installer)
            .WithMany()
            .HasForeignKey(m => m.InstallerId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

/// <summary>EF Core mapping for <see cref="StoreAccess"/>.</summary>
public class StoreAccessConfiguration : IEntityTypeConfiguration<StoreAccess>
{
    public void Configure(EntityTypeBuilder<StoreAccess> builder)
    {
        builder.ToTable("store_access");
        builder.HasKey(a => new { a.TenantMembershipId, a.StoreId });

        builder.HasOne(a => a.Membership)
            .WithMany(m => m.Stores)
            .HasForeignKey(a => a.TenantMembershipId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Store)
            .WithMany()
            .HasForeignKey(a => a.StoreId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
