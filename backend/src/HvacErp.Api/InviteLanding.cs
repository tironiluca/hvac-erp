namespace HvacErp.Api;

/// <summary>Where the link in an invitation e-mail lands.</summary>
public static class InviteLanding
{
    /// <summary>
    /// The set-password page of the SPA the request came from. The Origin header is used only when
    /// it is one of the configured frontend origins (else the first configured one), so the link
    /// can never be pointed at someone else's site. Null when no frontend origin is configured.
    /// </summary>
    public static string? For(HttpRequest request, IConfiguration configuration)
    {
        var allowed = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        var origin = request.Headers.Origin.ToString();
        var site = allowed.FirstOrDefault(o => string.Equals(o, origin, StringComparison.OrdinalIgnoreCase)) ?? allowed.FirstOrDefault();
        return string.IsNullOrWhiteSpace(site) ? null : $"{site.TrimEnd('/')}/auth/set-password";
    }
}
