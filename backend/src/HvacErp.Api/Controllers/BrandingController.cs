using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HvacErp.Api.Authorization;
using HvacErp.Application.Dtos;
using HvacErp.Application.Interfaces;

namespace HvacErp.Api.Controllers;

/// <summary>White-label portal branding (<c>/api/branding</c>): anonymous read for first paint; edit and asset upload by a platform administrator, because the branding is shared by every tenant until it becomes a per-tenant setting.</summary>
[ApiController]
[Authorize]
[Route("api/branding")]
public class BrandingController(IBrandingService branding) : ControllerBase
{
    /// <summary>Anonymous — the login page and first paint theme the app before any sign-in.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PortalBrandingDto>> Get(CancellationToken cancellationToken) =>
        Ok(await branding.GetAsync(cancellationToken));

    /// <summary>Replaces the brand colours, title and asset URLs. Platform administrator only.</summary>
    [HttpPut]
    [Authorize(Policy = Policies.PlatformAdmin)]
    public async Task<ActionResult<PortalBrandingDto>> Update(
        UpdatePortalBrandingRequest request, CancellationToken cancellationToken) =>
        Ok(await branding.UpdateAsync(request, cancellationToken));

    /// <summary>Uploads a logo / dark logo / favicon / login background (<paramref name="kind"/>) to storage and links it to the branding record. Platform administrator only; 5 MB max.</summary>
    [HttpPost("assets/{kind}")]
    [Authorize(Policy = Policies.PlatformAdmin)]
    [RequestSizeLimit(5_000_000)]
    public async Task<ActionResult<BrandingAssetDto>> UploadAsset(
        string kind, IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var asset = await branding.UploadAssetAsync(kind, stream, file.ContentType, file.FileName, cancellationToken);
        return Ok(asset);
    }
}
