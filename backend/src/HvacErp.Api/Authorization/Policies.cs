using Microsoft.AspNetCore.Authorization;
using HvacErp.Application.Common;
using HvacErp.Domain.Enums;

namespace HvacErp.Api.Authorization;

/// <summary>
/// Named authorization policies over the claims stamped by <c>SupabaseUserClaimsTransformation</c>.
/// Access matrix:
/// <list type="bullet">
/// <item>Everything needs a membership in the tenant the request works in (the default and fallback
/// policy) — being signed in is not enough. Only <see cref="SignedIn"/> endpoints (the caller's own
/// profile) answer someone without one.</item>
/// <item>Reads of business data (GET) — any member.</item>
/// <item>Creating / editing / deleting clients, quotes, repairs, installations, F-Gas records and
/// generating their PDFs — <see cref="SalesOrAdmin"/>.</item>
/// <item>A tenant's settings (PDF templates) and deleting clients (master data) —
/// <see cref="AdminOnly"/>.</item>
/// <item>Settings shared by every tenant (portal branding) — <see cref="PlatformAdmin"/>.</item>
/// <item><see cref="WarehouseOrAdmin"/> is reserved for the warehouse module.</item>
/// </list>
/// Roles are per tenant: the role claim is the caller's role in the tenant of this request.
/// </summary>
public static class Policies
{
    public const string AdminOnly = nameof(AdminOnly);
    public const string SalesOrAdmin = nameof(SalesOrAdmin);
    public const string WarehouseOrAdmin = nameof(WarehouseOrAdmin);

    /// <summary>Any authenticated identity, with or without a tenant.</summary>
    public const string SignedIn = nameof(SignedIn);

    /// <summary>A platform administrator.</summary>
    public const string PlatformAdmin = nameof(PlatformAdmin);

    /// <summary>Registers the member default / fallback policy and the named policies.</summary>
    public static AuthorizationBuilder AddAppPolicies(this AuthorizationBuilder builder)
    {
        var member = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireClaim(AppClaimTypes.TenantId)
            .Build();

        return builder
            // [Authorize] with no policy, and any endpoint that forgets the attribute, both require a member.
            .SetDefaultPolicy(member)
            .SetFallbackPolicy(member)
            .AddPolicy(SignedIn, policy => policy.RequireAuthenticatedUser())
            .AddPolicy(PlatformAdmin, policy => policy.RequireAuthenticatedUser().RequireClaim(AppClaimTypes.PlatformAdmin, "true"))
            .AddPolicy(AdminOnly, policy => policy.RequireClaim(AppClaimTypes.TenantId).RequireRole(nameof(UserRole.Admin)))
            .AddPolicy(SalesOrAdmin, policy => policy.RequireClaim(AppClaimTypes.TenantId).RequireRole(nameof(UserRole.Admin), nameof(UserRole.Sales)))
            .AddPolicy(WarehouseOrAdmin, policy => policy.RequireClaim(AppClaimTypes.TenantId).RequireRole(nameof(UserRole.Admin), nameof(UserRole.Warehouse)));
    }
}
