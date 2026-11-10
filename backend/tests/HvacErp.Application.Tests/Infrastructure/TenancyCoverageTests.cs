using Microsoft.EntityFrameworkCore;
using Npgsql;
using HvacErp.Domain.Entities;
using HvacErp.Infrastructure.Persistence;
using HvacErp.Infrastructure.Persistence.Migrations;

namespace HvacErp.Application.Tests.Infrastructure;

/// <summary>
/// The migration's hand-written table lists against the EF model. A new tenant-owned table, or a
/// new reference between two of them, must be added to a migration's lists (and so get its
/// <c>tenant_id</c> key, composite foreign key and policies) or these fail.
/// </summary>
public class TenancyCoverageTests
{
    private static AppDbContext Model()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(new NpgsqlDataSourceBuilder("Host=unused").Build())
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public void Every_tenant_owned_table_is_classified_by_a_migration()
    {
        using var db = Model();
        var entities = db.Model.GetEntityTypes().ToList();

        var tenantOwned = entities.Where(e => typeof(ITenantOwned).IsAssignableFrom(e.ClrType)).Select(e => e.GetTableName()!).ToHashSet();
        var storeOwned = entities.Where(e => typeof(IStoreOwned).IsAssignableFrom(e.ClrType)).Select(e => e.GetTableName()!).ToHashSet();
        var global = entities.Select(e => e.GetTableName()!).Except(tenantOwned).ToHashSet();

        // stores / tenant_memberships / store_access are created with their tenant_id by the migration.
        var classified = MultiTenancy.TenantOwnedTables.Concat(MultiTenancy.SecuredTables.Except(["tenants"])).ToHashSet();
        Assert.Empty(tenantOwned.Except(classified));
        Assert.Empty(classified.Except(tenantOwned));
        Assert.Equal(storeOwned.Order(), MultiTenancy.StoreTables.Order());

        // Whatever is not tenant-owned is shared on purpose: say so in the migration, or own it.
        string[] sharedOnPurpose = [.. MultiTenancy.PlatformTables, "tenants", "users", "mock_erp_orders"];
        Assert.Empty(global.Except(sharedOnPurpose));
    }

    [Fact]
    public void Every_reference_between_tenant_owned_tables_has_a_composite_foreign_key()
    {
        using var db = Model();

        var expected = db.Model.GetEntityTypes()
            .Where(e => typeof(ITenantOwned).IsAssignableFrom(e.ClrType))
            .SelectMany(e => e.GetForeignKeys()
                .Where(fk => typeof(ITenantOwned).IsAssignableFrom(fk.PrincipalEntityType.ClrType))
                .Select(fk => new TenantForeignKey(
                    e.GetTableName()!,
                    fk.Properties.Single().GetColumnName(),
                    fk.PrincipalEntityType.GetTableName()!,
                    fk.DeleteBehavior switch
                    {
                        DeleteBehavior.Cascade => TenantDelete.Cascade,
                        DeleteBehavior.SetNull => TenantDelete.SetNull,
                        _ => TenantDelete.Restrict,
                    })))
            .OrderBy(k => k.Table).ThenBy(k => k.Column)
            .ToList();

        Assert.Equal(expected, MultiTenancy.ForeignKeys.OrderBy(k => k.Table).ThenBy(k => k.Column));
    }

    [Fact]
    public void Every_tenant_owned_table_has_row_level_security_policies()
    {
        string[] withPolicies =
        [
            .. MultiTenancy.CatalogueTables, .. MultiTenancy.MasterDataTables, .. MultiTenancy.StoreTables,
            .. MultiTenancy.ChildTables.Select(c => c.Table), .. MultiTenancy.AppendOnlyChildTables.Select(c => c.Table),
        ];

        Assert.Equal(MultiTenancy.TenantOwnedTables.Order(), withPolicies.Order());
        Assert.Equal(withPolicies.Length, withPolicies.Distinct().Count());
    }
}
