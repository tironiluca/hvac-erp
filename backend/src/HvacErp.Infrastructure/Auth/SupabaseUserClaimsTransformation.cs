using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using HvacErp.Application.Common;

namespace HvacErp.Infrastructure.Auth;

/// <summary>
/// Runs after a Supabase-issued JWT is validated. Asks <see cref="TenantAccessResolver"/> what the
/// identity may do in the tenant / store the request selected (<see cref="AppClaimTypes.TenantHeader"/>,
/// <see cref="AppClaimTypes.StoreHeader"/>; the query string for SignalR) and writes the answer onto
/// the principal: role, tenant, readable stores, active store. Policies and the database context
/// read only these claims, so one place decides who a request works for.
/// </summary>
public class SupabaseUserClaimsTransformation(TenantAccessResolver resolver, IHttpContextAccessor accessor) : IClaimsTransformation
{
    /// <inheritdoc />
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true || principal.HasClaim(c => c.Type == AppClaimTypes.UserId))
        {
            return principal;
        }

        var subject = principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (subject is null || !Guid.TryParse(subject, out var userId))
        {
            return principal;
        }

        var request = accessor.HttpContext?.Request;
        var access = await resolver.ResolveAsync(
            userId,
            principal.FindFirstValue("email"),
            ReadSelection(request, AppClaimTypes.TenantHeader, AppClaimTypes.TenantQuery),
            ReadSelection(request, AppClaimTypes.StoreHeader, AppClaimTypes.StoreQuery),
            accessor.HttpContext?.RequestAborted ?? default);

        var identity = (ClaimsIdentity)principal.Identity;
        identity.AddClaim(new Claim(AppClaimTypes.UserId, access.UserId.ToString()));
        if (access.IsPlatformAdmin)
        {
            identity.AddClaim(new Claim(AppClaimTypes.PlatformAdmin, "true"));
        }

        if (access.TenantId is { } tenantId && access.Role is { } role)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role.ToString()));
            identity.AddClaim(new Claim(AppClaimTypes.TenantId, tenantId.ToString(CultureInfo.InvariantCulture)));
            foreach (var storeId in access.StoreIds)
            {
                identity.AddClaim(new Claim(AppClaimTypes.StoreId, storeId.ToString(CultureInfo.InvariantCulture)));
            }
            if (access.ActiveStoreId is { } activeStoreId)
            {
                identity.AddClaim(new Claim(AppClaimTypes.ActiveStoreId, activeStoreId.ToString(CultureInfo.InvariantCulture)));
            }
        }

        return principal;
    }

    /// <summary>
    /// The id a request selected. Anything that is not a positive integer counts as a selection that
    /// cannot be honoured (0), so a malformed header fails closed instead of falling back to a default.
    /// </summary>
    private static int? ReadSelection(HttpRequest? request, string header, string query)
    {
        if (request is null)
        {
            return null;
        }

        string? raw = request.Headers[header];
        if (string.IsNullOrWhiteSpace(raw) && request.Path.StartsWithSegments("/hubs"))
        {
            raw = request.Query[query];
        }
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0 ? id : 0;
    }
}
