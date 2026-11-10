using Microsoft.EntityFrameworkCore;
using HvacErp.Domain.Entities;

namespace HvacErp.Application.Common;

/// <summary>
/// Persistence-agnostic view of the database used by Application services.
/// Implemented by HvacErp.Infrastructure's EF Core AppDbContext.
/// </summary>
public interface IAppDbContext
{
    /// <summary>
    /// The tenant and stores this context filters on and stamps. Services read it when a rule depends
    /// on the store being worked in (quote numbering, which store a new record goes to).
    /// </summary>
    ITenantContext Tenant { get; }

    DbSet<Tenant> Tenants { get; }
    DbSet<Store> Stores { get; }
    DbSet<TenantMembership> TenantMemberships { get; }
    DbSet<StoreAccess> StoreAccesses { get; }
    DbSet<AppUser> Users { get; }
    DbSet<Installer> Installers { get; }
    DbSet<Operator> Operators { get; }
    DbSet<ClientSource> ClientSources { get; }
    DbSet<ProductType> ProductTypes { get; }
    DbSet<ProductPriceList> ProductPriceLists { get; }
    DbSet<PriceListDiscountTier> PriceListDiscountTiers { get; }
    DbSet<Product> Products { get; }
    DbSet<ServiceItem> ServiceItems { get; }
    DbSet<ExtraType> ExtraTypes { get; }
    DbSet<IncentiveScheme> IncentiveSchemes { get; }
    DbSet<Client> Clients { get; }

    DbSet<VatOption> VatOptions { get; }
    DbSet<RepairStatus> RepairStatuses { get; }
    DbSet<QuoteStatus> QuoteStatuses { get; }
    DbSet<DuctSystemType> DuctSystemTypes { get; }
    DbSet<QuoteDepartment> QuoteDepartments { get; }
    DbSet<DaySlot> DaySlots { get; }

    DbSet<Quote> Quotes { get; }
    DbSet<QuoteProduct> QuoteProducts { get; }
    DbSet<DuctedUnit> DuctedUnits { get; }
    DbSet<DuctedUnitOutlet> DuctedUnitOutlets { get; }
    DbSet<QuoteServiceItem> QuoteServiceItems { get; }
    DbSet<QuoteExtra> QuoteExtras { get; }
    DbSet<QuoteIncentive> QuoteIncentives { get; }
    DbSet<ErpOrderExport> ErpOrderExports { get; }
    DbSet<QuoteStatusEvent> QuoteStatusEvents { get; }
    DbSet<QuoteAttachment> QuoteAttachments { get; }

    DbSet<Repair> Repairs { get; }
    DbSet<RepairServiceItem> RepairServiceItems { get; }
    DbSet<RepairScheduleEntry> RepairScheduleEntries { get; }
    DbSet<RepairStatusEvent> RepairStatusEvents { get; }

    DbSet<FGasRecord> FGasRecords { get; }

    DbSet<ScheduledInstallation> ScheduledInstallations { get; }
    DbSet<InstallationInstaller> InstallationInstallers { get; }

    DbSet<PortalBranding> PortalBrandings { get; }
    DbSet<PdfTemplate> PdfTemplates { get; }

    /// <summary>Persists tracked changes.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
