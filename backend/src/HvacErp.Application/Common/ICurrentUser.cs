using HvacErp.Domain.Enums;

namespace HvacErp.Application.Common;

/// <summary>
/// The authenticated caller behind the current request, used to stamp audit columns
/// (<c>ChangedBy</c> on the quote/repair status-event history). Implemented in the API layer
/// from the Supabase JWT principal. Every member is null when the call is unauthenticated or
/// runs outside an HTTP request (data seeding, background jobs, tests).
/// </summary>
public interface ICurrentUser
{
    /// <summary>Supabase user id — the JWT <c>sub</c> / our <c>app_user_id</c> claim.</summary>
    string? Id { get; }

    /// <summary>Human-readable identity (email), falling back to <see cref="Id"/> when no email claim is present.</summary>
    string? Name { get; }

    /// <summary>The caller's <see cref="UserRole"/> (from the <c>AppUser</c> profile), or null when unknown.</summary>
    UserRole? Role { get; }
}

/// <summary>Role checks over <see cref="ICurrentUser"/> for Application-layer authorization.</summary>
public static class CurrentUserExtensions
{
    /// <summary>
    /// Application-layer role gate for sub-operations, backing up the controller policies (defense in
    /// depth). A null <see cref="ICurrentUser.Id"/> means a trusted system context (seeding, background
    /// work) and is allowed; an identified caller must hold one of <paramref name="allowed"/>.
    /// </summary>
    /// <exception cref="ForbiddenException">The caller is identified but lacks every allowed role.</exception>
    public static void EnsureInRole(this ICurrentUser user, string operation, params UserRole[] allowed)
    {
        if (user.Id is null)
        {
            return;
        }

        if (user.Role is not { } role || !allowed.Contains(role))
        {
            throw new ForbiddenException($"Your role is not allowed to {operation}.");
        }
    }
}
