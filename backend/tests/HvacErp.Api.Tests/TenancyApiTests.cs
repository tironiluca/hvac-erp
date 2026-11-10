using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using HvacErp.Application.Dtos;
using HvacErp.Domain.Entities;
using HvacErp.Domain.Enums;
using HvacErp.Infrastructure.Persistence;

namespace HvacErp.Api.Tests;

/// <summary>Tenant isolation and membership-based access through the real HTTP pipeline.</summary>
public class TenancyApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static UpsertClientRequest NewClient(string code, string name) =>
        new(code, name, null, null, null, null, null, null, null, null, null, null);

    [Theory]
    [InlineData("/api/clients")]
    [InlineData("/api/quotes")]
    [InlineData("/api/repairs")]
    [InlineData("/api/installations")]
    [InlineData("/api/dashboard")]
    [InlineData("/api/lookups/products")]
    [InlineData("/api/lookups/vat-options")]
    public async Task Signed_in_without_a_membership_is_forbidden_everywhere(string url)
    {
        var response = await factory.ClientAs(ApiFactory.NoMembership).GetAsync(url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Signed_in_without_a_membership_can_still_read_their_own_profile()
    {
        var me = await factory.ClientAs(ApiFactory.NoMembership).GetFromJsonAsync<CurrentUserDto>("/api/me");

        Assert.NotNull(me);
        Assert.Null(me.Role);
        Assert.Null(me.TenantId);
        Assert.Empty(me.Tenants);
    }

    [Fact]
    public async Task A_tenants_clients_do_not_exist_for_another_tenant()
    {
        var alfa = factory.ClientAs("Admin", tenantId: 1, stores: "1");
        var beta = factory.ClientAs("Admin", tenantId: 2, stores: "3");

        var created = await (await alfa.PostAsJsonAsync("/api/clients", NewClient("ISO-1", "Only Alfa's")))
            .Content.ReadFromJsonAsync<ClientDto>();
        Assert.NotNull(created);

        Assert.Contains(await alfa.GetFromJsonAsync<List<ClientDto>>("/api/clients?search=ISO-1") ?? [], c => c.Id == created.Id);
        Assert.Empty(await beta.GetFromJsonAsync<List<ClientDto>>("/api/clients?search=ISO-1") ?? []);
        Assert.Equal(HttpStatusCode.NotFound, (await beta.GetAsync($"/api/clients/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await beta.PutAsJsonAsync($"/api/clients/{created.Id}", NewClient("ISO-1", "Taken over"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await beta.DeleteAsync($"/api/clients/{created.Id}")).StatusCode);

        // The same code is free in the other company, and the original is untouched.
        Assert.Equal(HttpStatusCode.Created, (await beta.PostAsJsonAsync("/api/clients", NewClient("ISO-1", "Beta's own"))).StatusCode);
        Assert.Equal("Only Alfa's", (await alfa.GetFromJsonAsync<ClientDto>($"/api/clients/{created.Id}"))!.FullName);
    }

    [Theory]
    [InlineData(false, HttpStatusCode.Forbidden)]
    [InlineData(true, HttpStatusCode.OK)]
    public async Task Shared_branding_is_edited_by_a_platform_administrator_not_a_tenant_admin(bool platformAdmin, HttpStatusCode expected)
    {
        var current = await factory.ClientAs(null).GetFromJsonAsync<PortalBrandingDto>("/api/branding");
        var update = new UpdatePortalBrandingRequest(current!.PrimarySeed, current.TertiarySeed, current.NeutralSeed, current.PortalTitle);

        var response = await factory.ClientAs("Admin", platformAdmin: platformAdmin).PutAsJsonAsync("/api/branding", update);

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Profile_lists_the_tenants_and_stores_the_caller_may_switch_to()
    {
        var userId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            // No request behind this scope: seeding goes through the system context and names every owner.
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            using var system = db.AsSystem();
            db.Tenants.AddRange(new Tenant { Id = 11, Name = "Alfa" }, new Tenant { Id = 12, Name = "Beta" }, new Tenant { Id = 13, Name = "Suspended", IsActive = false });
            db.Stores.AddRange(
                new Store { Id = 21, TenantId = 11, Name = "Centre", City = "Bergamo" },
                new Store { Id = 22, TenantId = 11, Name = "North" },
                new Store { Id = 23, TenantId = 11, Name = "Closed", IsActive = false },
                new Store { Id = 24, TenantId = 12, Name = "Beta one" },
                new Store { Id = 25, TenantId = 12, Name = "Beta two" });
            db.Users.Add(new AppUser { Id = userId, Email = "anna@example.com", DisplayName = "Anna", IsPlatformAdmin = true });
            db.TenantMemberships.AddRange(
                new TenantMembership { TenantId = 11, UserId = userId, Role = UserRole.Admin, AllStores = true },
                new TenantMembership
                {
                    TenantId = 12, UserId = userId, Role = UserRole.Sales,
                    Stores = { new StoreAccess { TenantId = 12, StoreId = 25 } },
                },
                new TenantMembership { TenantId = 13, UserId = userId, Role = UserRole.Admin, AllStores = true });
            await db.SaveChangesAsync();
        }

        var client = factory.ClientAs("Admin", tenantId: 11, stores: "21");
        client.DefaultRequestHeaders.Add(ApiFactory.UserHeader, userId.ToString());
        var me = await client.GetFromJsonAsync<CurrentUserDto>("/api/me");

        Assert.NotNull(me);
        Assert.Equal(("Admin", 11, 21, true), (me.Role, me.TenantId, me.StoreId, me.IsPlatformAdmin));
        Assert.Equal(["Alfa", "Beta"], me.Tenants.Select(t => t.Name));
        Assert.Equal(["Centre", "North"], me.Tenants[0].Stores.Select(s => s.Name)); // every active store
        Assert.Equal(("Sales", "Beta two"), (me.Tenants[1].Role, me.Tenants[1].Stores.Single().Name)); // only the granted one
    }

    /// <summary>
    /// The whole chain a real request takes: a bare Supabase identity, the membership lookup, the
    /// tenant / store headers, the policies and the query filters.
    /// </summary>
    [Fact]
    public async Task Sign_in_resolution_honours_memberships_and_the_selected_tenant_and_store()
    {
        var userId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            using var system = db.AsSystem();
            db.Tenants.AddRange(new Tenant { Id = 31, Name = "Gamma" }, new Tenant { Id = 32, Name = "Delta" }, new Tenant { Id = 33, Name = "Not hers" });
            db.Stores.AddRange(
                new Store { Id = 41, TenantId = 31, Name = "Gamma east" }, new Store { Id = 42, TenantId = 31, Name = "Gamma west" },
                new Store { Id = 43, TenantId = 32, Name = "Delta" }, new Store { Id = 44, TenantId = 33, Name = "Not hers" });
            db.Users.Add(new AppUser { Id = userId, Email = "gina@example.com", DisplayName = "Gina" });
            db.TenantMemberships.AddRange(
                new TenantMembership { TenantId = 31, UserId = userId, Role = UserRole.Sales, AllStores = true },
                new TenantMembership { TenantId = 32, UserId = userId, Role = UserRole.Warehouse, AllStores = true });
            db.Clients.AddRange(
                new Client { TenantId = 31, Code = "G1", FullName = "Gamma client" },
                new Client { TenantId = 32, Code = "D1", FullName = "Delta client" },
                new Client { TenantId = 33, Code = "N1", FullName = "Not her client" });
            await db.SaveChangesAsync();
        }

        HttpClient As(int? tenantId = null, int? storeId = null)
        {
            var client = factory.CreateClient();
            client.DefaultRequestHeaders.Add(ApiFactory.SupabaseIdentityHeader, userId.ToString());
            if (tenantId is not null) client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
            if (storeId is not null) client.DefaultRequestHeaders.Add("X-Store-Id", storeId.ToString());
            return client;
        }

        async Task<string[]> ClientNames(HttpClient client) =>
            [.. (await client.GetFromJsonAsync<List<ClientDto>>("/api/clients"))!.Select(c => c.FullName)];

        // No selection: her first tenant, every store, none active because there are two.
        var first = await As().GetFromJsonAsync<CurrentUserDto>("/api/me");
        Assert.Equal(("Sales", 31, null), (first!.Role, first.TenantId, first.StoreId));
        Assert.Equal(["Gamma client"], await ClientNames(As()));

        // Selecting the other tenant switches data and role with it.
        Assert.Equal(["Delta client"], await ClientNames(As(tenantId: 32)));
        Assert.Equal(HttpStatusCode.Forbidden,
            (await As(tenantId: 32).PostAsJsonAsync("/api/clients", NewClient("D2", "By the warehouse"))).StatusCode);

        // Selecting a store makes it the active one.
        var narrowed = await As(tenantId: 31, storeId: 42).GetFromJsonAsync<CurrentUserDto>("/api/me");
        Assert.Equal(42, narrowed!.StoreId);

        // A tenant she is not in, or a store of another tenant: no access, never a fallback.
        Assert.Equal(HttpStatusCode.Forbidden, (await As(tenantId: 33).GetAsync("/api/clients")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await As(tenantId: 31, storeId: 43).GetAsync("/api/clients")).StatusCode);

        // Someone the system has never seen gets a profile and nothing else.
        var stranger = factory.CreateClient();
        stranger.DefaultRequestHeaders.Add(ApiFactory.SupabaseIdentityHeader, Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.GetAsync("/api/clients")).StatusCode);
        Assert.Empty((await stranger.GetFromJsonAsync<CurrentUserDto>("/api/me"))!.Tenants);
    }
}
