using System.Globalization;
using System.Security.Claims;
using HvacErp.Application.Common;

namespace HvacErp.Api;

/// <summary>
/// <see cref="ITenantContext"/> of the current HTTP request, read from the claims
/// <c>SupabaseUserClaimsTransformation</c> resolved. Never the system context: code that runs with
/// no request behind it (workers, seeding) has no claims, so it sees nothing until it explicitly
/// asks the database context for <c>AsSystem()</c>.
/// </summary>
public sealed class RequestTenantContext(IHttpContextAccessor accessor) : ITenantContext
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    /// <inheritdoc />
    public bool IsSystem => false;

    /// <inheritdoc />
    public int? TenantId => ParseId(Principal?.FindFirstValue(AppClaimTypes.TenantId));

    /// <inheritdoc />
    public IReadOnlyCollection<int> StoreIds =>
        Principal?.FindAll(AppClaimTypes.StoreId).Select(c => ParseId(c.Value)).OfType<int>().ToArray() ?? [];

    /// <inheritdoc />
    public int? StoreId => ParseId(Principal?.FindFirstValue(AppClaimTypes.ActiveStoreId));

    private static int? ParseId(string? value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0 ? id : null;
}
