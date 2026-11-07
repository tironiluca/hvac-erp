using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using HvacErp.Domain.Entities;
using HvacErp.Domain.Enums;
using HvacErp.Infrastructure.Persistence;

namespace HvacErp.Infrastructure.Auth;

/// <summary>
/// Runs after a Supabase-issued JWT is validated. Looks up (or creates, on first sight) the
/// matching AppUser profile row and stamps a role claim onto the principal so
/// [Authorize(Roles = "...")] works against our own UserRole, not anything Supabase knows about.
/// </summary>
public class SupabaseUserClaimsTransformation(AppDbContext context) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return principal;
        }

        if (principal.HasClaim(c => c.Type == ClaimTypes.Role))
        {
            return principal;
        }

        var subject = principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (subject is null || !Guid.TryParse(subject, out var userId))
        {
            return principal;
        }

        var appUser = await context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (appUser is null)
        {
            appUser = new AppUser
            {
                Id = userId,
                Email = principal.FindFirstValue("email") ?? string.Empty,
                DisplayName = principal.FindFirstValue("email") ?? string.Empty,
                Role = UserRole.Sales
            };
            context.Users.Add(appUser);
            await context.SaveChangesAsync();
        }

        var identity = (ClaimsIdentity)principal.Identity;
        identity.AddClaim(new Claim(ClaimTypes.Role, appUser.Role.ToString()));
        identity.AddClaim(new Claim("app_user_id", appUser.Id.ToString()));

        return principal;
    }
}
