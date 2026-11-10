namespace HvacErp.Application.Common;

/// <summary>
/// Creates sign-in accounts. Authentication belongs to the identity provider (Supabase Auth); this
/// is the one thing the application asks of it besides validating tokens.
/// </summary>
public interface IIdentityInviter
{
    /// <summary>
    /// Creates an account for <paramref name="email"/> and e-mails its owner a link to choose a
    /// password, landing on <paramref name="redirectTo"/> when given.
    /// </summary>
    /// <returns>The new account's id — the id its tokens will carry.</returns>
    /// <exception cref="ConflictException">An account with that e-mail already exists.</exception>
    /// <exception cref="BusinessRuleException">The provider refused the address.</exception>
    Task<Guid> InviteAsync(string email, string? redirectTo, CancellationToken cancellationToken = default);
}
