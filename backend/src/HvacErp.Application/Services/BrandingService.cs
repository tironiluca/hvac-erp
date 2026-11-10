using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using HvacErp.Application.Common;
using HvacErp.Application.Dtos;
using HvacErp.Application.Interfaces;
using HvacErp.Domain.Entities;

namespace HvacErp.Application.Services;

/// <summary>Reads and edits the one-row portal branding and uploads its assets.</summary>
public partial class BrandingService(
    IAppDbContext context,
    IFileStorageService storage,
    ICurrentUser currentUser) : IBrandingService
{
    private const int SingletonId = 1;

    private static readonly HashSet<string> AssetKinds =
        new(StringComparer.OrdinalIgnoreCase) { "logo", "logo-dark", "favicon", "login-background" };

    // content type -> file extension for the image formats we accept.
    private static readonly Dictionary<string, string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/png"] = ".png",
        ["image/jpeg"] = ".jpg",
        ["image/webp"] = ".webp",
        ["image/svg+xml"] = ".svg",
        ["image/x-icon"] = ".ico",
        ["image/vnd.microsoft.icon"] = ".ico",
    };

    public async Task<PortalBrandingDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var row = await context.PortalBrandings.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == SingletonId, cancellationToken);
        return ToDto(row ?? new PortalBranding());
    }

    public async Task<PortalBrandingDto> UpdateAsync(
        UpdatePortalBrandingRequest request, CancellationToken cancellationToken = default)
    {
        var primary = NormalizeHex(request.PrimarySeed, nameof(request.PrimarySeed));
        var tertiary = NormalizeHex(request.TertiarySeed, nameof(request.TertiarySeed));
        var neutral = NormalizeHex(request.NeutralSeed, nameof(request.NeutralSeed));

        var title = request.PortalTitle?.Trim();
        if (string.IsNullOrEmpty(title))
        {
            throw new BusinessRuleException("Portal title must not be empty.");
        }
        if (title.Length > 200)
        {
            throw new BusinessRuleException("Portal title must be at most 200 characters.");
        }

        var row = await GetOrCreateRowAsync(cancellationToken);
        row.PrimarySeed = primary;
        row.TertiarySeed = tertiary;
        row.NeutralSeed = neutral;
        row.PortalTitle = title;
        Stamp(row);

        await context.SaveChangesAsync(cancellationToken);
        return ToDto(row);
    }

    public async Task<BrandingAssetDto> UploadAssetAsync(
        string kind, Stream content, string contentType, string? fileName, CancellationToken cancellationToken = default)
    {
        if (!AssetKinds.Contains(kind))
        {
            throw new BusinessRuleException(
                $"Unknown branding asset '{kind}'. Expected one of: {string.Join(", ", AssetKinds)}.");
        }

        var extension = ResolveExtension(contentType, fileName)
            ?? throw new BusinessRuleException(
                "Unsupported image type. Use PNG, JPEG, WebP, SVG or ICO.");

        var normalizedKind = kind.ToLowerInvariant();
        var path = $"branding/{normalizedKind}-{DateTime.UtcNow:yyyyMMddHHmmssfff}{extension}";
        // Shown on the login page before anyone signs in: a public asset by nature.
        var url = await storage.UploadPublicAsync(path, content, contentType, cancellationToken);

        var row = await GetOrCreateRowAsync(cancellationToken);
        switch (normalizedKind)
        {
            case "logo": row.LogoUrl = url; break;
            case "logo-dark": row.LogoDarkUrl = url; break;
            case "favicon": row.FaviconUrl = url; break;
            case "login-background": row.LoginBackgroundUrl = url; break;
        }
        Stamp(row);

        await context.SaveChangesAsync(cancellationToken);
        return new BrandingAssetDto(url);
    }

    private async Task<PortalBranding> GetOrCreateRowAsync(CancellationToken cancellationToken)
    {
        var row = await context.PortalBrandings.FirstOrDefaultAsync(b => b.Id == SingletonId, cancellationToken);
        if (row is null)
        {
            row = new PortalBranding { Id = SingletonId };
            context.PortalBrandings.Add(row);
        }
        return row;
    }

    private void Stamp(PortalBranding row)
    {
        row.UpdatedAt = DateTime.UtcNow;
        row.UpdatedBy = currentUser.Name;
    }

    private static string NormalizeHex(string? value, string field)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (!HexColor().IsMatch(trimmed))
        {
            throw new BusinessRuleException($"{field} must be a hex colour in #RRGGBB form.");
        }
        return trimmed.ToUpperInvariant();
    }

    private static string? ResolveExtension(string? contentType, string? fileName)
    {
        if (!string.IsNullOrWhiteSpace(contentType) && AllowedContentTypes.TryGetValue(contentType.Trim(), out var ext))
        {
            return ext;
        }

        var fromName = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        return AllowedContentTypes.Values.Contains(fromName) || fromName == ".jpeg" ? Normalize(fromName) : null;

        static string Normalize(string e) => e == ".jpeg" ? ".jpg" : e;
    }

    private static PortalBrandingDto ToDto(PortalBranding b) => new(
        b.PrimarySeed, b.TertiarySeed, b.NeutralSeed, b.PortalTitle,
        b.LogoUrl, b.LogoDarkUrl, b.FaviconUrl, b.LoginBackgroundUrl, b.UpdatedAt);

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColor();
}
