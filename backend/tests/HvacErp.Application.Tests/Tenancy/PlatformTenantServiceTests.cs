using Microsoft.EntityFrameworkCore;
using HvacErp.Application.Common;
using HvacErp.Application.Dtos;
using HvacErp.Application.Services;
using HvacErp.Application.Tests.Support;
using HvacErp.Domain.Entities;
using HvacErp.Domain.Enums;
using HvacErp.Infrastructure.Auth;
using HvacErp.Infrastructure.Persistence;
using HvacErp.Infrastructure.Tenancy;
using Microsoft.Extensions.Configuration;

namespace HvacErp.Application.Tests.Tenancy;

/// <summary>The platform administrator creating and suspending companies.</summary>
public class PlatformTenantServiceTests
{
    private readonly string _database = $"platform-{Guid.NewGuid():N}";
    private readonly ITenantContext _callerTenant = FixedTenantContext.For(1, 1);
    private readonly AppDbContext _db;
    private readonly FakeInviter _inviter = new();
    private readonly PlatformTenantService _sut;

    public PlatformTenantServiceTests()
    {
        using (var seed = TestDb.Create(FixedTenantContext.System, _database))
        {
            seed.Tenants.Add(new Tenant { Id = 1, Name = "Alfa" });
            seed.Stores.Add(new Store { Id = 1, TenantId = 1, Name = "Centre" });
            seed.Clients.Add(new Client { TenantId = 1, Code = "A1", FullName = "Alfa's client" });
            seed.SaveChanges();
        }

        // The administrator is working in tenant 1 while managing the others.
        _db = TestDb.Create(_callerTenant, _database);
        _sut = new PlatformTenantService(_db, _callerTenant, _inviter, new FakeCurrentUser("root@example.com"));
    }

    private static CreateTenantRequest NewTenant(string name = "Beta Service", string admin = "owner@beta.example") =>
        new(name, "Beta Service S.r.l.", "IT01234567890", "Beta main", admin);

    [Fact]
    public async Task Creating_a_company_gives_it_a_store_its_defaults_and_an_invited_admin()
    {
        var created = await _sut.CreateTenantAsync(NewTenant(), "https://app.example/auth/set-password");

        Assert.Equal(("Beta Service", true, 1, 1), (created.Name, created.IsActive, created.Stores, created.Members));
        var (email, redirectTo, accountId) = Assert.Single(_inviter.Invitations);
        Assert.Equal(("owner@beta.example", "https://app.example/auth/set-password"), (email, redirectTo));

        using var system = _db.AsSystem();
        var store = await _db.Stores.SingleAsync(s => s.TenantId == created.Id);
        Assert.Equal("Beta main", store.Name);

        // What the pricing engine and the PDFs need, owned by the new tenant.
        var serviceCodes = await _db.ServiceItems.Where(s => s.TenantId == created.Id && s.Code != null).Select(s => s.Code!).ToListAsync();
        Assert.Contains(InstallationCostItems.BaseSystemPriceFor(1), serviceCodes);
        Assert.NotEmpty(await _db.ExtraTypes.Where(e => e.TenantId == created.Id).ToListAsync());
        Assert.NotEmpty(await _db.IncentiveSchemes.Where(i => i.TenantId == created.Id).ToListAsync());
        Assert.Equal(Enum.GetValues<PdfDocumentType>().Length, await _db.PdfTemplates.CountAsync(t => t.TenantId == created.Id && t.IsActive));

        // Its first Admin, with every store.
        var membership = await _db.TenantMemberships.SingleAsync(m => m.TenantId == created.Id);
        Assert.Equal((accountId, UserRole.Admin, true), (membership.UserId, membership.Role, membership.AllStores));
    }

    [Fact]
    public async Task The_new_admin_signs_in_to_their_company_and_sees_nothing_of_the_others()
    {
        var created = await _sut.CreateTenantAsync(NewTenant(), null);
        var adminId = _inviter.Invitations.Single().AccountId;

        var access = await new TenantAccessResolver(TestDb.Create(FixedTenantContext.None, _database), new ConfigurationBuilder().Build())
            .ResolveAsync(adminId, "owner@beta.example", null, null);

        Assert.Equal((created.Id, UserRole.Admin), (access.TenantId, access.Role));
        var asNewAdmin = TestDb.Create(FixedTenantContext.For(created.Id, access.StoreIds), _database);
        Assert.Empty(await asNewAdmin.Clients.ToListAsync());
        Assert.Single(await asNewAdmin.Stores.ToListAsync());
    }

    [Fact]
    public async Task Creating_a_company_leaves_the_administrators_own_company_as_it_was()
    {
        await _sut.CreateTenantAsync(NewTenant(), null);

        // Back in the caller's tenant once the work is done: same rows, nothing of the new tenant.
        Assert.Equal(["Alfa's client"], await _db.Clients.Select(c => c.FullName).ToListAsync());
        Assert.Equal(["Centre"], await _db.Stores.Select(s => s.Name).ToListAsync());
        Assert.Empty(await _db.ServiceItems.ToListAsync());
    }

    [Fact]
    public async Task An_existing_person_can_be_made_admin_of_a_second_company()
    {
        var first = await _sut.CreateTenantAsync(NewTenant(), null);
        var second = await _sut.CreateTenantAsync(NewTenant("Gamma Clima", "owner@beta.example"), null);

        Assert.Single(_inviter.Invitations); // invited once, added twice
        using var system = _db.AsSystem();
        Assert.Equal([first.Id, second.Id], await _db.TenantMemberships
            .Where(m => m.UserId == _inviter.Invitations[0].AccountId).OrderBy(m => m.TenantId).Select(m => m.TenantId).ToListAsync());
    }

    [Theory]
    [InlineData("", "Main", "owner@beta.example")]
    [InlineData("Beta", " ", "owner@beta.example")]
    [InlineData("Beta", "Main", "not-an-email")]
    public async Task A_company_needs_a_name_a_store_and_a_valid_admin(string name, string store, string email)
    {
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => _sut.CreateTenantAsync(new CreateTenantRequest(name, null, null, store, email), null));
        Assert.Empty(_inviter.Invitations);
    }

    [Fact]
    public async Task Company_names_are_unique()
    {
        await Assert.ThrowsAsync<ConflictException>(() => _sut.CreateTenantAsync(NewTenant("alfa"), null));

        var beta = await _sut.CreateTenantAsync(NewTenant(), null);
        await Assert.ThrowsAsync<ConflictException>(
            () => _sut.UpdateTenantAsync(beta.Id, new UpdateTenantRequest("ALFA", null, null, true)));
    }

    [Fact]
    public async Task Every_company_is_listed_with_its_size_suspended_ones_included()
    {
        var beta = await _sut.CreateTenantAsync(NewTenant(), null);
        await _sut.UpdateTenantAsync(beta.Id, new UpdateTenantRequest("Beta Service", null, "IT999", IsActive: false));

        var list = await _sut.GetTenantsAsync();

        Assert.Equal([("Alfa", true, 1, 0), ("Beta Service", false, 1, 1)], list.Select(t => (t.Name, t.IsActive, t.Stores, t.Members)));
        Assert.Equal("IT999", list[1].VatNumber);
    }

    [Fact]
    public async Task Suspending_a_company_locks_its_members_out_and_resuming_lets_them_back()
    {
        var beta = await _sut.CreateTenantAsync(NewTenant(), null);
        var adminId = _inviter.Invitations.Single().AccountId;
        TenantAccessResolver Resolver() => new(TestDb.Create(FixedTenantContext.None, _database), new ConfigurationBuilder().Build());

        await _sut.UpdateTenantAsync(beta.Id, new UpdateTenantRequest("Beta Service", null, null, IsActive: false));
        Assert.Null((await Resolver().ResolveAsync(adminId, null, null, null)).TenantId);

        await _sut.UpdateTenantAsync(beta.Id, new UpdateTenantRequest("Beta Service", null, null, IsActive: true));
        Assert.Equal(beta.Id, (await Resolver().ResolveAsync(adminId, null, null, null)).TenantId);
    }

    [Fact]
    public async Task The_company_being_worked_in_cannot_be_suspended_from_under_its_administrator()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => _sut.UpdateTenantAsync(1, new UpdateTenantRequest("Alfa", null, null, IsActive: false)));
        await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.UpdateTenantAsync(999, new UpdateTenantRequest("Nobody", null, null, true)));
    }
}
