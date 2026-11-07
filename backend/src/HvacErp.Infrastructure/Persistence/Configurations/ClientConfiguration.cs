using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HvacErp.Domain.Entities;

namespace HvacErp.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="Client"/>.</summary>
public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.HasIndex(c => c.Code).IsUnique();
        builder.Property(c => c.Code).HasMaxLength(20).IsRequired();
        builder.Property(c => c.FullName).HasMaxLength(200).IsRequired();
        builder.Property(c => c.TaxCode).HasMaxLength(20);
        builder.Property(c => c.Phone).HasMaxLength(30);
        builder.Property(c => c.Mobile).HasMaxLength(30);
        builder.Property(c => c.Email).HasMaxLength(150);

        builder.HasOne(c => c.Source)
            .WithMany()
            .HasForeignKey(c => c.SourceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(c => c.ReferredByClient)
            .WithMany()
            .HasForeignKey(c => c.ReferredByClientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
