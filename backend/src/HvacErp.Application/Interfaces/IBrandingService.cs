using HvacErp.Application.Dtos;

namespace HvacErp.Application.Interfaces;

/// <summary>Reads and mutates the single <c>PortalBranding</c> row (seed colours, title, assets).</summary>
public interface IBrandingService
{
    /// <summary>The current branding, or the built-in defaults when the row has never been saved.</summary>
    Task<PortalBrandingDto> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Validates the three seed hexes, upserts row 1, stamps the auditing columns.</summary>
    Task<PortalBrandingDto> UpdateAsync(UpdatePortalBrandingRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads one branding asset to storage and records its public URL on row 1.
    /// <paramref name="kind"/> is one of <c>logo</c>, <c>logo-dark</c>, <c>favicon</c>, <c>login-background</c>.
    /// </summary>
    Task<BrandingAssetDto> UploadAssetAsync(
        string kind, Stream content, string contentType, string? fileName, CancellationToken cancellationToken = default);
}
