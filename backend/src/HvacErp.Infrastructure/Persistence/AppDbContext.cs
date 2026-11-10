using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.DependencyInjection;
using HvacErp.Application.Common;
using HvacErp.Infrastructure.Erp;
using HvacErp.Domain.Entities;

namespace HvacErp.Infrastructure.Persistence;

/// <summary>
/// EF Core context over Supabase Postgres; implements <see cref="Application.Common.IAppDbContext"/>
/// and stores every <see cref="DateTime"/> as UTC.
/// <para>
/// <b>Tenant isolation lives here.</b> Every <see cref="ITenantOwned"/> entity gets a global query
/// filter on the <see cref="ITenantContext"/>'s tenant (and, for <see cref="IStoreOwned"/>, its
/// stores); every save stamps the tenant / store on new rows and refuses a write into another
/// tenant. A context without a tenant sees no tenant-owned row and can insert none — the safe
/// default for anything that forgot to say who it works for.
/// </para>
/// </summary>
public class AppDbContext : DbContext, IAppDbContext
{
    private readonly ITenantContext _tenant;
    private int _systemScopes;

    /// <summary>A context with no tenant: tenant-owned sets read empty and refuse inserts.</summary>
    public AppDbContext(DbContextOptions<AppDbContext> options) : this(options, FixedTenantContext.None)
    {
    }

    /// <summary>A context working for <paramref name="tenant"/> (resolved per request by the API).</summary>
    [ActivatorUtilitiesConstructor]
    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenant) : base(options)
    {
        _tenant = tenant;
    }

    /// <inheritdoc />
    public ITenantContext Tenant => _tenant;

    // Read by the global query filters; EF evaluates context members per query, so one cached
    // model serves every tenant.
    private bool TenantFilterOff => _systemScopes > 0 || _tenant.IsSystem;
    private int CurrentTenantId => _tenant.TenantId ?? 0;
    private int[] CurrentStoreIds => _tenant.StoreIds as int[] ?? [.. _tenant.StoreIds];

    /// <summary>
    /// Switches this context to the trusted cross-tenant mode until the returned scope is disposed:
    /// filters off, nothing stamped, so every row written must carry its tenant. For in-process code
    /// with no caller behind it (outbox workers, sign-in resolution, seeding) — never for a request's
    /// business logic, which is why it is not on <see cref="IAppDbContext"/>.
    /// </summary>
    public IDisposable AsSystem()
    {
        _systemScopes++;
        return new SystemScope(this);
    }

    private sealed class SystemScope(AppDbContext context) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                context._systemScopes--;
            }
        }
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();
    public DbSet<StoreAccess> StoreAccesses => Set<StoreAccess>();
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
    public DbSet<ErpOrderExport> ErpOrderExports => Set<ErpOrderExport>();
    public DbSet<MockErpOrder> MockErpOrders => Set<MockErpOrder>();
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

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Select(e => e.ClrType).ToList())
        {
            var filter = typeof(IStoreOwned).IsAssignableFrom(entityType) ? nameof(ApplyStoreFilter)
                : typeof(ITenantOwned).IsAssignableFrom(entityType) ? nameof(ApplyTenantFilter)
                : null;
            if (filter is not null)
            {
                typeof(AppDbContext).GetMethod(filter, BindingFlags.Instance | BindingFlags.NonPublic)!
                    .MakeGenericMethod(entityType)
                    .Invoke(this, [modelBuilder]);
            }
        }
    }

    private void ApplyTenantFilter<T>(ModelBuilder modelBuilder) where T : class, ITenantOwned =>
        modelBuilder.Entity<T>()
            .HasQueryFilter(e => TenantFilterOff || e.TenantId == CurrentTenantId);

    private void ApplyStoreFilter<T>(ModelBuilder modelBuilder) where T : class, IStoreOwned =>
        modelBuilder.Entity<T>()
            .HasQueryFilter(e => TenantFilterOff || (e.TenantId == CurrentTenantId && CurrentStoreIds.Contains(e.StoreId)))
            .HasIndex(e => new { e.TenantId, e.StoreId });

    /// <inheritdoc />
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyTenantRules();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <inheritdoc />
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyTenantRules();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Stamps the tenant (and store) on new rows and refuses any write outside the caller's tenant
    /// and stores. The query filters already keep foreign rows out of reach; this catches the cases
    /// a filter cannot — an id set by hand, an attached entity, a row moved between tenants.
    /// </summary>
    private void ApplyTenantRules()
    {
        var system = TenantFilterOff;
        foreach (var entry in ChangeTracker.Entries<ITenantOwned>())
        {
            switch (entry.State)
            {
                case EntityState.Added when system:
                    if (entry.Entity.TenantId == 0)
                    {
                        throw new InvalidOperationException(
                            $"{entry.Metadata.ClrType.Name} was added in the system context without a tenant.");
                    }
                    break;
                case EntityState.Added:
                    StampNew(entry);
                    break;
                case EntityState.Modified or EntityState.Deleted when !system:
                    GuardExisting(entry);
                    break;
            }
        }
    }

    private void StampNew(EntityEntry<ITenantOwned> entry)
    {
        var tenantId = _tenant.RequireTenantId();
        if (entry.Entity.TenantId == 0)
        {
            entry.Entity.TenantId = tenantId;
        }
        else if (entry.Entity.TenantId != tenantId)
        {
            throw new ForbiddenException("You cannot write to another company's data.");
        }

        if (entry.Entity is IStoreOwned stored)
        {
            if (stored.StoreId == 0)
            {
                stored.StoreId = _tenant.RequireStoreId();
            }
            else if (!_tenant.CanAccessStore(stored.StoreId))
            {
                throw new ForbiddenException("You do not work in that store.");
            }
        }
    }

    private void GuardExisting(EntityEntry<ITenantOwned> entry)
    {
        var tenantId = _tenant.RequireTenantId();
        var tenant = entry.Property(e => e.TenantId);
        if (tenant.OriginalValue != tenantId || tenant.CurrentValue != tenantId)
        {
            throw new ForbiddenException("You cannot write to another company's data.");
        }

        if (entry.Entity is IStoreOwned stored)
        {
            var originalStore = (int)entry.Property(nameof(IStoreOwned.StoreId)).OriginalValue!;
            if (!_tenant.CanAccessStore(originalStore) || !_tenant.CanAccessStore(stored.StoreId))
            {
                throw new ForbiddenException("You do not work in that store.");
            }
        }
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
