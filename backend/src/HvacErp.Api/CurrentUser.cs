using System.Security.Claims;
using HvacErp.Application.Common;
using HvacErp.Domain.Enums;

namespace HvacErp.Api;

/// <summary>
/// <see cref="ICurrentUser"/> backed by the current request's Supabase JWT principal. The claims
/// are those left by <c>SupabaseUserClaimsTransformation</c> (raw names — <c>MapInboundClaims</c>
/// is off): <c>sub</c>, <c>email</c>, plus the added <c>app_user_id</c> / role claims.
/// </summary>
public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public string? Id =>
        Principal?.FindFirstValue(AppClaimTypes.UserId)
        ?? Principal?.FindFirstValue("sub")
        ?? Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? Name
    {
        get
        {
            var email = Principal?.FindFirstValue("email");
            return string.IsNullOrWhiteSpace(email) ? Id : email;
        }
    }

    public UserRole? Role =>
        Enum.TryParse<UserRole>(Principal?.FindFirstValue(ClaimTypes.Role), out var role) ? role : null;
}
