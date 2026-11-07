using Microsoft.AspNetCore.Authorization;
using HvacErp.Domain.Enums;

namespace HvacErp.Api.Authorization;

/// <summary>
/// Named authorization policies, keyed off the <see cref="UserRole"/> claim stamped by
/// <c>SupabaseUserClaimsTransformation</c>. Access matrix:
/// <list type="bullet">
/// <item>Reads of business data (GET) — any authenticated user (<c>[Authorize]</c>).</item>
/// <item>Creating / editing / deleting clients, quotes, repairs, installations, F-Gas records and
/// generating their PDFs — <see cref="SalesOrAdmin"/>.</item>
/// <item>Portal settings (branding, PDF templates) and deleting clients (master data) —
/// <see cref="AdminOnly"/>.</item>
/// <item><see cref="WarehouseOrAdmin"/> is reserved for the upcoming Magazzino module.</item>
/// </list>
/// </summary>
public static class Policies
{
    public const string AdminOnly = nameof(AdminOnly);
    public const string SalesOrAdmin = nameof(SalesOrAdmin);
    public const string WarehouseOrAdmin = nameof(WarehouseOrAdmin);

    /// <summary>Registers the fallback (authenticated) policy plus <see cref="AdminOnly"/>, <see cref="SalesOrAdmin"/> and <see cref="WarehouseOrAdmin"/>.</summary>
    public static AuthorizationBuilder AddAppPolicies(this AuthorizationBuilder builder) => builder
        // Any endpoint that forgets an [Authorize] still requires a signed-in user.
        .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
        .AddPolicy(AdminOnly, policy => policy.RequireRole(nameof(UserRole.Admin)))
        .AddPolicy(SalesOrAdmin, policy => policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Sales)))
        .AddPolicy(WarehouseOrAdmin, policy => policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Warehouse)));
}
