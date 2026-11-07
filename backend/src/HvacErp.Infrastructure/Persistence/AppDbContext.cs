using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using HvacErp.Application.Common;
using HvacErp.Domain.Entities;

namespace HvacErp.Infrastructure.Persistence;

/// <summary>EF Core context over Supabase Postgres; implements <see cref="Application.Common.IAppDbContext"/> and stores every <see cref="DateTime"/> as UTC.</summary>
public class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Installer> Installers => Set<Installer>();
    public DbSet<Operator> Operators => Set<Operator>();
    public DbSet<ClientSource> ClientSources => Set<ClientSource>();
    public DbSet<ProductType> ProductTypes => Set<ProductType>();
    public DbSet<ProductPriceList> ProductPriceLists => Set<ProductPriceList>();
    public DbSet<PriceListDiscountTier> PriceListDiscountTiers => Set<PriceListDiscountTier>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ServiceItem> ServiceItems => Set<ServiceItem>();
    public DbSet<ExtraType> ExtraTypes => Set<ExtraType>();
    public DbSet<IncentiveScheme> IncentiveSchemes => Set<IncentiveScheme>();
    public DbSet<Client> Clients => Set<Client>();

    public DbSet<VatOption> VatOptions => Set<VatOption>();
    public DbSet<RepairStatus> RepairStatuses => Set<RepairStatus>();
    public DbSet<QuoteStatus> QuoteStatuses => Set<QuoteStatus>();
    public DbSet<DuctSystemType> DuctSystemTypes => Set<DuctSystemType>();
    public DbSet<QuoteDepartment> QuoteDepartments => Set<QuoteDepartment>();
    public DbSet<DaySlot> DaySlots => Set<DaySlot>();

    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<QuoteProduct> QuoteProducts => Set<QuoteProduct>();
    public DbSet<DuctedUnit> DuctedUnits => Set<DuctedUnit>();
    public DbSet<DuctedUnitOutlet> DuctedUnitOutlets => Set<DuctedUnitOutlet>();
    public DbSet<QuoteServiceItem> QuoteServiceItems => Set<QuoteServiceItem>();
    public DbSet<QuoteExtra> QuoteExtras => Set<QuoteExtra>();
    public DbSet<QuoteIncentive> QuoteIncentives => Set<QuoteIncentive>();
    public DbSet<QuoteProductIncentive> QuoteProductIncentives => Set<QuoteProductIncentive>();
    public DbSet<QuoteStatusEvent> QuoteStatusEvents => Set<QuoteStatusEvent>();
    public DbSet<QuoteAttachment> QuoteAttachments => Set<QuoteAttachment>();

    public DbSet<Repair> Repairs => Set<Repair>();
    public DbSet<RepairServiceItem> RepairServiceItems => Set<RepairServiceItem>();
    public DbSet<RepairScheduleEntry> RepairScheduleEntries => Set<RepairScheduleEntry>();
    public DbSet<RepairStatusEvent> RepairStatusEvents => Set<RepairStatusEvent>();

    public DbSet<FGasRecord> FGasRecords => Set<FGasRecord>();

    public DbSet<ScheduledInstallation> ScheduledInstallations => Set<ScheduledInstallation>();
    public DbSet<InstallationInstaller> InstallationInstallers => Set<InstallationInstaller>();

    public DbSet<PortalBranding> PortalBrandings => Set<PortalBranding>();
    public DbSet<PdfTemplate> PdfTemplates => Set<PdfTemplate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HaveColumnType("numeric(12,2)");

        // All columns are `timestamp with time zone`, which Npgsql only accepts Kind=Utc for.
        // DTOs deserialized from JSON without an offset (e.g. date-only values) come in as
        // Kind=Unspecified, so normalize every DateTime to UTC on the way in/out rather than
        // relying on every call site to remember to do it.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }

    private class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        v => ToUtc(v),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    private class NullableUtcDateTimeConverter() : ValueConverter<DateTime?, DateTime?>(
        v => v.HasValue ? ToUtc(v.Value) : v,
        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
}
