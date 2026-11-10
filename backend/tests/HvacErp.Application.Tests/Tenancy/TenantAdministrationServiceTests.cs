using Microsoft.EntityFrameworkCore;
using HvacErp.Application.Common;
using HvacErp.Application.Dtos;
using HvacErp.Application.Services;
using HvacErp.Application.Tests.Support;
using HvacErp.Domain.Entities;
using HvacErp.Domain.Enums;
using HvacErp.Infrastructure.Persistence;

namespace HvacErp.Application.Tests.Tenancy;

/// <summary>A tenant's Admin managing their own stores and members — and nobody else's.</summary>
public class TenantAdministrationServiceTests
{
    private static readonly Guid AdminId = Guid.Parse("00000000-0000-0000-0000-0000000000a1");

    private readonly string _database = $"admin-{Guid.NewGuid():N}";
    private readonly AppDbContext _db;
    private readonly FakeInviter _inviter = new();
    private readonly TenantAdministrationService _sut;

    public TenantAdministrationServiceTests()
    {
        // Tenant 1 with stores 1 and 2 and one Admin; tenant 2 with store 3.
        using (var seed = TestDb.Create(FixedTenantContext.System, _database))
        {
            seed.Tenants.AddRange(new Tenant { Id = 1, Name = "Alfa" }, new Tenant { Id = 2, Name = "Beta" });
            seed.Stores.AddRange(
                new Store { Id = 1, TenantId = 1, Name = "Centre" },
                new Store { Id = 2, TenantId = 1, Name = "North" },
                new Store { Id = 3, TenantId = 2, Name = "Beta main" });
            seed.Users.Add(new AppUser { Id = AdminId, Email = "admin@alfa.example", DisplayName = "Admin" });
            seed.TenantMemberships.Add(new TenantMembership { TenantId = 1, UserId = AdminId, Role = UserRole.Admin, AllStores = true });
            seed.SaveChanges();
        }

        _db = TestDb.Create(FixedTenantContext.For(1, [1, 2]), _database);
        _sut = Service(_db, UserRole.Admin);
    }

    private TenantAdministrationService Service(AppDbContext db, UserRole role, Guid? userId = null) =>
        new(db, new FakeCurrentUser(id: (userId ?? AdminId).ToString(), role: role), _inviter);

    [Fact]
    public async Task Stores_are_listed_added_renamed_and_deactivated_within_the_tenant()
    {
        var created = await _sut.CreateStoreAsync(new UpsertStoreRequest("  South ", null, " Lecco ", null, null));
        Assert.Equal(("South", "Lecco", true), (created.Name, created.City, created.IsActive));

        var updated = await _sut.UpdateStoreAsync(created.Id, new UpsertStoreRequest("South", "Via Roma 1", "Lecco", null, null, IsActive: false));
        Assert.False(updated.IsActive);

        Assert.Equal(["Centre", "North", "South"], (await _sut.GetStoresAsync()).Select(s => s.Name)); // inactive ones stay listed
        using var system = _db.AsSystem();
        Assert.Equal(1, (await _db.Stores.SingleAsync(s => s.Id == created.Id)).TenantId);
    }

    [Fact]
    public async Task A_store_name_is_unique_in_the_tenant_but_free_in_another()
    {
        await Assert.ThrowsAsync<ConflictException>(() => _sut.CreateStoreAsync(new UpsertStoreRequest("centre", null, null, null, null)));
        await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.CreateStoreAsync(new UpsertStoreRequest("  ", null, null, null, null)));

        var created = await _sut.CreateStoreAsync(new UpsertStoreRequest("Beta main", null, null, null, null)); // tenant 2 has one too
        Assert.Equal("Beta main", created.Name);
    }

    [Fact]
    public async Task The_last_active_store_cannot_be_deactivated_and_another_tenants_store_does_not_exist()
    {
        await _sut.UpdateStoreAsync(2, new UpsertStoreRequest("North", null, null, null, null, IsActive: false));

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => _sut.UpdateStoreAsync(1, new UpsertStoreRequest("Centre", null, null, null, null, IsActive: false)));
        await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.UpdateStoreAsync(3, new UpsertStoreRequest("Taken over", null, null, null, null)));
    }

    [Fact]
    public async Task Someone_without_an_account_is_invited_and_becomes_a_member()
    {
        var member = await _sut.AddMemberAsync(
            new AddMemberRequest(" New.Colleague@Alfa.example ", UserRole.Sales, AllStores: false, StoreIds: [2]),
            "https://app.example/auth/set-password");

        var (email, redirectTo, accountId) = Assert.Single(_inviter.Invitations);
        Assert.Equal(("new.colleague@alfa.example", "https://app.example/auth/set-password"), (email, redirectTo));
        Assert.Equal((accountId, "Sales", false), (member.UserId, member.Role, member.AllStores));
        Assert.Equal([2], member.StoreIds);

        using var system = _db.AsSystem();
        var stored = await _db.TenantMemberships.Include(m => m.Stores).SingleAsync(m => m.UserId == accountId);
        Assert.Equal(1, stored.TenantId);
        Assert.Equal((1, 2), (stored.Stores.Single().TenantId, stored.Stores.Single().StoreId));
    }

    [Fact]
    public async Task Someone_who_already_has_a_profile_is_added_without_an_invitation()
    {
        var existing = Guid.NewGuid();
        using (var seed = TestDb.Create(FixedTenantContext.System, _database))
        {
            // Signed in once (so the profile exists) and is a member of tenant 2.
            seed.Users.Add(new AppUser { Id = existing, Email = "bruno@beta.example", DisplayName = "Bruno" });
            seed.TenantMemberships.Add(new TenantMembership { TenantId = 2, UserId = existing, Role = UserRole.Admin, AllStores = true });
            seed.SaveChanges();
        }

        var member = await _sut.AddMemberAsync(new AddMemberRequest("Bruno@beta.example", UserRole.Warehouse, AllStores: true), null);

        Assert.Empty(_inviter.Invitations);
        Assert.Equal((existing, "Warehouse"), (member.UserId, member.Role));
        await Assert.ThrowsAsync<ConflictException>(
            () => _sut.AddMemberAsync(new AddMemberRequest("bruno@beta.example", UserRole.Sales, AllStores: true), null));

        // His membership of the other tenant is untouched, and invisible from here.
        Assert.Equal(["admin@alfa.example", "bruno@beta.example"], (await _sut.GetMembersAsync()).Select(m => m.Email));
        using var system = _db.AsSystem();
        Assert.Equal(UserRole.Admin, (await _db.TenantMemberships.SingleAsync(m => m.UserId == existing && m.TenantId == 2)).Role);
    }

    [Fact]
    public async Task An_account_that_never_signed_in_here_has_to_sign_in_first()
    {
        _inviter.ExistingAccounts.Add("known@elsewhere.example");

        var error = await Assert.ThrowsAsync<ConflictException>(
            () => _sut.AddMemberAsync(new AddMemberRequest("known@elsewhere.example", UserRole.Sales, AllStores: true), null));

        Assert.Contains("sign in once", error.Message);
        Assert.Single(await _sut.GetMembersAsync());
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("")]
    [InlineData("two@addresses.example, other@addresses.example")]
    public async Task An_invalid_email_is_refused_before_anyone_is_invited(string email)
    {
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => _sut.AddMemberAsync(new AddMemberRequest(email, UserRole.Sales, AllStores: true), null));
        Assert.Empty(_inviter.Invitations);
    }

    [Fact]
    public async Task Store_grants_must_name_stores_of_this_tenant()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => _sut.AddMemberAsync(new AddMemberRequest("a@alfa.example", UserRole.Sales, AllStores: false, StoreIds: []), null));
        await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.AddMemberAsync(new AddMemberRequest("a@alfa.example", UserRole.Sales, AllStores: false, StoreIds: [1, 3]), null)); // 3 is tenant 2's
        Assert.Empty(_inviter.Invitations);
    }

    [Fact]
    public async Task A_members_role_and_stores_can_be_changed()
    {
        var member = await _sut.AddMemberAsync(new AddMemberRequest("c@alfa.example", UserRole.Sales, AllStores: false, StoreIds: [1]), null);

        var updated = await _sut.UpdateMemberAsync(member.Id, new UpdateMemberRequest(UserRole.Warehouse, AllStores: false, StoreIds: [2]));
        Assert.Equal(("Warehouse", false), (updated.Role, updated.AllStores));
        Assert.Equal([2], updated.StoreIds);

        updated = await _sut.UpdateMemberAsync(member.Id, new UpdateMemberRequest(UserRole.Admin, AllStores: true));
        Assert.Equal(("Admin", true), (updated.Role, updated.AllStores));
        Assert.Empty(updated.StoreIds);
    }

    [Fact]
    public async Task The_tenant_always_keeps_an_admin()
    {
        var onlyAdmin = (await _sut.GetMembersAsync()).Single();
        Assert.True(onlyAdmin.IsYou);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => _sut.UpdateMemberAsync(onlyAdmin.Id, new UpdateMemberRequest(UserRole.Sales, AllStores: true)));
        await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.RemoveMemberAsync(onlyAdmin.Id));

        // With a second Admin in place the first can step down or leave.
        var second = await _sut.AddMemberAsync(new AddMemberRequest("second@alfa.example", UserRole.Admin, AllStores: true), null);
        await _sut.RemoveMemberAsync(onlyAdmin.Id);
        Assert.Equal([second.Id], (await _sut.GetMembersAsync()).Select(m => m.Id));
    }

    [Fact]
    public async Task Removing_a_member_keeps_their_account()
    {
        var member = await _sut.AddMemberAsync(new AddMemberRequest("leaver@alfa.example", UserRole.Sales, AllStores: false, StoreIds: [1]), null);

        await _sut.RemoveMemberAsync(member.Id);

        using var system = _db.AsSystem();
        Assert.NotNull(await _db.Users.SingleOrDefaultAsync(u => u.Id == member.UserId));
        Assert.Empty(await _db.StoreAccesses.ToListAsync());
    }

    [Fact]
    public async Task Another_tenants_members_are_out_of_reach()
    {
        var betaDb = TestDb.Create(FixedTenantContext.For(2, 3), _database);
        var betaAdmin = Service(betaDb, UserRole.Admin, Guid.NewGuid());
        var alfaAdminMembership = (await _sut.GetMembersAsync()).Single().Id;

        Assert.Empty(await betaAdmin.GetMembersAsync());
        await Assert.ThrowsAsync<NotFoundException>(() => betaAdmin.RemoveMemberAsync(alfaAdminMembership));
        await Assert.ThrowsAsync<NotFoundException>(
            () => betaAdmin.UpdateMemberAsync(alfaAdminMembership, new UpdateMemberRequest(UserRole.Warehouse, AllStores: true)));
    }

    [Theory]
    [InlineData(UserRole.Sales)]
    [InlineData(UserRole.Warehouse)]
    public async Task Only_an_admin_administers(UserRole role)
    {
        var sut = Service(_db, role);

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.GetMembersAsync());
        await Assert.ThrowsAsync<ForbiddenException>(() => sut.GetStoresAsync());
        await Assert.ThrowsAsync<ForbiddenException>(() => sut.CreateStoreAsync(new UpsertStoreRequest("X", null, null, null, null)));
        await Assert.ThrowsAsync<ForbiddenException>(() => sut.AddMemberAsync(new AddMemberRequest("x@alfa.example", UserRole.Admin, AllStores: true), null));
    }
}
