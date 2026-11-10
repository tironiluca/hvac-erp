using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HvacErp.Application.Common;

namespace HvacErp.Infrastructure.Auth;

/// <summary>
/// <see cref="IIdentityInviter"/> over Supabase Auth's admin API (<c>POST /auth/v1/invite</c>, with
/// the service-role key the HttpClient is configured with). Supabase creates the account and sends
/// the invitation e-mail; the link in it must be allowed under Auth → URL Configuration.
/// </summary>
public class SupabaseIdentityInviter(HttpClient http) : IIdentityInviter
{
    /// <inheritdoc />
    public async Task<Guid> InviteAsync(string email, string? redirectTo, CancellationToken cancellationToken = default)
    {
        var url = string.IsNullOrWhiteSpace(redirectTo)
            ? "auth/v1/invite"
            : $"auth/v1/invite?redirect_to={Uri.EscapeDataString(redirectTo)}";

        using var response = await http.PostAsJsonAsync(url, new { email }, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            using var user = JsonDocument.Parse(body);
            return user.RootElement.TryGetProperty("id", out var id) && Guid.TryParse(id.GetString(), out var accountId)
                ? accountId
                : throw new InvalidOperationException("Supabase Auth returned no account id for the invitation.");
        }

        var (code, message) = ReadError(body);
        if (code == "email_exists" || message.Contains("already been registered", StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException($"An account for {email} already exists.");
        }

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new BusinessRuleException("Too many invitations were sent in a short time. Try again in a few minutes.");
        }

        if ((int)response.StatusCode is >= 400 and < 500 && response.StatusCode is not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden))
        {
            // The provider's own wording for a refused address (invalid, blocked domain…).
            throw new BusinessRuleException(string.IsNullOrWhiteSpace(message) ? "The invitation was refused." : message);
        }

        // Misconfiguration (bad key) or an outage: not something the person inviting can fix.
        throw new HttpRequestException($"Supabase Auth invite failed with HTTP {(int)response.StatusCode}.");
    }

    private static (string Code, string Message) ReadError(string body)
    {
        try
        {
            using var error = JsonDocument.Parse(body);
            var root = error.RootElement;
            return (
                root.TryGetProperty("error_code", out var code) && code.ValueKind == JsonValueKind.String ? code.GetString()! : string.Empty,
                root.TryGetProperty("msg", out var msg) && msg.ValueKind == JsonValueKind.String ? msg.GetString()!
                    : root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String ? message.GetString()!
                    : string.Empty);
        }
        catch (JsonException)
        {
            return (string.Empty, string.Empty);
        }
    }
}
