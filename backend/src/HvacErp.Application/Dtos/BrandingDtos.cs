namespace HvacErp.Application.Dtos;

/// <summary>The portal's current white-label branding (<c>GET /api/branding</c> — anonymous).</summary>
public record PortalBrandingDto(
    string PrimarySeed,
    string TertiarySeed,
    string NeutralSeed,
    string PortalTitle,
    string? LogoUrl,
    string? LogoDarkUrl,
    string? FaviconUrl,
    string? LoginBackgroundUrl,
    DateTime UpdatedAt);

/// <summary>Admin edit of the seed colours + title (<c>PUT /api/branding</c>). Assets are set via upload.</summary>
public record UpdatePortalBrandingRequest(
    string PrimarySeed,
    string TertiarySeed,
    string NeutralSeed,
    string PortalTitle);

/// <summary>The public URL of a freshly uploaded branding asset.</summary>
public record BrandingAssetDto(string Url);

/// <summary>The signed-in caller's app profile, for the frontend to gate admin-only UI (<c>GET /api/me</c>).</summary>
public record CurrentUserDto(string Email, string Role);
