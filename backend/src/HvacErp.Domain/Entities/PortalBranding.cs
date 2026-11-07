namespace HvacErp.Domain.Entities;

/// <summary>
/// The portal's white-label branding — a single row (<see cref="Id"/> always <c>1</c>). Holds the
/// three Material 3 seed colours an admin sets in-app plus the branding assets (logo, favicon, login
/// background) and the portal title. Applied at runtime as CSS custom properties on <c>&lt;html&gt;</c>
/// by the frontend's <c>BrandingService</c>; the compile-time SCSS theme is only the fallback.
/// </summary>
public class PortalBranding
{
    /// <summary>Product-level default title, used until an admin sets the deployment's own.</summary>
    public const string DefaultPortalTitle = "HVAC ERP";

    /// <summary>Fixed single-row key. Always <c>1</c> — never generated.</summary>
    public int Id { get; set; } = 1;

    /// <summary>Primary brand seed, <c>#RRGGBB</c> (default <c>#2B6CB0</c>, a neutral blue).</summary>
    public string PrimarySeed { get; set; } = "#2B6CB0";

    /// <summary>Tertiary / CTA-accent seed, <c>#RRGGBB</c> (default <c>#DD6B20</c>, amber).</summary>
    public string TertiarySeed { get; set; } = "#DD6B20";

    /// <summary>Neutral surface/text ramp seed, <c>#RRGGBB</c> (default <c>#5F6368</c>, grey).</summary>
    public string NeutralSeed { get; set; } = "#5F6368";

    /// <summary>Shown in the toolbar, the sidenav header, the browser tab and the login card.</summary>
    public string PortalTitle { get; set; } = DefaultPortalTitle;

    /// <summary>Public URL of the company logo (sidenav + login). Null → the text title is shown.</summary>
    public string? LogoUrl { get; set; }

    /// <summary>Optional dark-theme logo variant; falls back to <see cref="LogoUrl"/> when null.</summary>
    public string? LogoDarkUrl { get; set; }

    /// <summary>Public URL of the favicon (overrides the static <c>favicon.ico</c> at runtime).</summary>
    public string? FaviconUrl { get; set; }

    /// <summary>Public URL of the login-page background image. Null → a subtle brand gradient.</summary>
    public string? LoginBackgroundUrl { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Supabase user id / email of the admin who last saved, when known.</summary>
    public string? UpdatedBy { get; set; }
}
