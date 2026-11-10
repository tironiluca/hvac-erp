using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using HvacErp.Application.Common;
using HvacErp.Application.Tests.Support;
using HvacErp.Domain.Entities;
using HvacErp.Domain.Enums;
using HvacErp.Infrastructure.Auth;
using HvacErp.Infrastructure.Persistence;

namespace HvacErp.Application.Tests.Tenancy;

/// <summary>Sign-in resolution: access comes from a membership and from nothing else.</summary>
public class TenantAccessResolverTests
{
    private static readonly Guid Anna = Guid.Parse("00000000-0000-0000-0000-00000000000a");

    // The request context before sign-in is resolved: no tenant yet.
    private readonly AppDbContext _db = TestDb.Create(FixedTenantContext.None);

    public TenantAccessResolverTests()
    {
        using var system = _db.AsSystem();
        _db.Tenants.AddRange(
            new Tenant { Id = 1, Name = "Alfa" },
            new Tenant { Id = 2, Name = "Beta" });
        _db.Stores.AddRange(
            new Store { Id = 1, TenantId = 1, Name = "Alfa centre" },
            new Store { Id = 2, TenantId = 1, Name = "Alfa north" },
            new Store { Id = 3, TenantId = 2, Name = "Beta" });
        _db.SaveChanges();
    }

    private TenantAccessResolver Resolver(string? bootstrapEmail = null) => new(_db, new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { [TenantAccessResolver.BootstrapAdminEmailKey] = bootstrapEmail })
        .Build());

    private void Grant(Guid userId, int tenantId, UserRole role, bool allStores = true, params int[] stores)
    {
        using var system = _db.AsSystem();
        if (!_db.Users.Any(u => u.Id == userId))
        {
            _db.Users.Add(new AppUser { Id = userId, Email = "anna@example.com", DisplayName = "Anna" });
        }

        var membership = new TenantMembership { TenantId = tenantId, UserId = userId, Role = role, AllStores = allStores };
        foreach (var storeId in stores)
        {
            membership.Stores.Add(new StoreAccess { TenantId = tenantId, StoreId = storeId });
        }
        _db.TenantMemberships.Add(membership);
        _db.SaveChanges();
    }

    [Fact]
    public async Task An_identity_seen_for_the_first_time_gets_a_profile_and_no_access()
    {
        var access = await Resolver().ResolveAsync(Anna, "anna@example.com", null, null);

        Assert.Null(access.TenantId);
        Assert.Null(access.Role);
        Assert.Empty(access.StoreIds);
        using var system = _db.AsSystem();
        Assert.Equal("anna@example.com", (await _db.Users.SingleAsync()).Email);
        Assert.Empty(await _db.TenantMemberships.ToListAsync());
    }

    [Fact]
    public async Task A_member_of_every_store_reads_all_of_them_and_must_choose_where_to_create()
    {
        Grant(Anna, 1, UserRole.Admin);

        var access = await Resolver().ResolveAsync(Anna, null, null, null);

        Assert.Equal((1, UserRole.Admin), (access.TenantId, access.Role));
        Assert.Equal([1, 2], access.StoreIds);
        Assert.Null(access.ActiveStoreId);
    }

    [Fact]
    public async Task A_member_of_one_store_works_in_it_without_choosing()
    {
        Grant(Anna, 1, UserRole.Sales, allStores: false, 2);

        var access = await Resolver().ResolveAsync(Anna, null, null, null);

        Assert.Equal([2], access.StoreIds);
        Assert.Equal(2, access.ActiveStoreId);
    }

    [Fact]
    public async Task Selecting_a_store_narrows_the_request_to_it()
    {
        Grant(Anna, 1, UserRole.Sales);

        var access = await Resolver().ResolveAsync(Anna, null, 1, 2);

        Assert.Equal([2], access.StoreIds);
        Assert.Equal(2, access.ActiveStoreId);
    }

    [Theory]
    [InlineData(2, null)] // a tenant she does not belong to
    [InlineData(1, 3)]    // another tenant's store
    [InlineData(1, 1)]    // a store of her tenant she was not given
    [InlineData(0, null)] // a malformed selection
    public async Task A_selection_that_cannot_be_honoured_grants_nothing(int? tenantId, int? storeId)
    {
        Grant(Anna, 1, UserRole.Sales, allStores: false, 2);

        var access = await Resolver().ResolveAsync(Anna, null, tenantId, storeId);

        Assert.Null(access.TenantId);
        Assert.Null(access.Role);
    }

    [Fact]
    public async Task The_role_is_the_one_held_in_the_selected_tenant()
    {
        Grant(Anna, 1, UserRole.Warehouse);
        Grant(Anna, 2, UserRole.Admin);

        Assert.Equal((1, UserRole.Warehouse), await Role(null));
        Assert.Equal((2, UserRole.Admin), await Role(2));

        async Task<(int?, UserRole?)> Role(int? tenantId)
        {
            var access = await Resolver().ResolveAsync(Anna, null, tenantId, null);
            return (access.TenantId, access.Role);
        }
    }

    [Fact]
    public async Task A_disabled_user_or_a_suspended_tenant_grants_nothing()
    {
        Grant(Anna, 1, UserRole.Admin);

        using (_db.AsSystem())
        {
            (await _db.Tenants.SingleAsync(t => t.Id == 1)).IsActive = false;
            await _db.SaveChangesAsync();
        }
        Assert.Null((await Resolver().ResolveAsync(Anna, null, null, null)).TenantId);

        using (_db.AsSystem())
        {
            (await _db.Tenants.SingleAsync(t => t.Id == 1)).IsActive = true;
            (await _db.Users.SingleAsync()).IsActive = false;
            await _db.SaveChangesAsync();
        }
        Assert.Null((await Resolver().ResolveAsync(Anna, null, null, null)).TenantId);
    }

    [Fact]
    public async Task The_bootstrap_email_becomes_the_first_administrator_once()
    {
        var first = await Resolver("Owner@Example.com").ResolveAsync(Anna, "owner@example.com", null, null);

        Assert.True(first.IsPlatformAdmin);
        Assert.Equal((1, UserRole.Admin), (first.TenantId, first.Role));
        Assert.Equal([1, 2], first.StoreIds);

        // With a platform administrator in place the setting is inert, whoever matches it.
        var other = Guid.NewGuid();
        using (_db.AsSystem())
        {
            (await _db.Users.SingleAsync()).Email = "renamed@example.com";
            await _db.SaveChangesAsync();
        }
        var second = await Resolver("owner@example.com").ResolveAsync(other, "owner@example.com", null, null);

        Assert.False(second.IsPlatformAdmin);
        Assert.Null(second.TenantId);
    }

    [Fact]
    public async Task Without_the_bootstrap_setting_nobody_is_promoted()
    {
        var access = await Resolver().ResolveAsync(Anna, "owner@example.com", null, null);

        Assert.False(access.IsPlatformAdmin);
        Assert.Null(access.TenantId);
    }
}
