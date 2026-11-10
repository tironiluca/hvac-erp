using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using HvacErp.Application.Common;
using HvacErp.Infrastructure.Persistence;

namespace HvacErp.Infrastructure.Services.Storage;

/// <summary>What <see cref="BrandingAssetMover"/> did with one branding asset.</summary>
/// <param name="Kind">Which asset (logo, dark logo, favicon, login background).</param>
/// <param name="Moved">True when it was copied to the public bucket and its link updated.</param>
/// <param name="Detail">The new URL, or why it was left alone.</param>
public sealed record BrandingAssetMove(string Kind, bool Moved, string Detail);

/// <summary>
/// One-off deployment step for the move to a private documents bucket. The logo, favicon and login
/// background used to be uploaded next to the documents; once that bucket is private their links
/// stop working. This copies each of them to the public assets bucket and points the branding row
/// at the copy. Safe to run before or after the bucket is made private, and safe to run twice:
/// an asset already in the public bucket is left alone.
/// </summary>
public class BrandingAssetMover(AppDbContext context, IFileStorageService storage, IConfiguration configuration)
{
    /// <summary>Moves every branding asset that still lives in the documents bucket.</summary>
    public async Task<IReadOnlyList<BrandingAssetMove>> MoveAsync(CancellationToken cancellationToken = default)
    {
        var legacyPrefix =
            $"{configuration["Supabase:Url"]?.TrimEnd('/')}/storage/v1/object/public/{configuration["Supabase:StorageBucket"]}/";

        var results = new List<BrandingAssetMove>();
        var row = await context.PortalBrandings.FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return results;
        }

        row.LogoUrl = await MoveOneAsync("logo", row.LogoUrl);
        row.LogoDarkUrl = await MoveOneAsync("logo-dark", row.LogoDarkUrl);
        row.FaviconUrl = await MoveOneAsync("favicon", row.FaviconUrl);
        row.LoginBackgroundUrl = await MoveOneAsync("login-background", row.LoginBackgroundUrl);
        await context.SaveChangesAsync(cancellationToken);
        return results;

        async Task<string?> MoveOneAsync(string kind, string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return url;
            }

            if (!url.StartsWith(legacyPrefix, StringComparison.Ordinal))
            {
                results.Add(new BrandingAssetMove(kind, false, "not in the documents bucket, left as it is"));
                return url;
            }

            var path = Uri.UnescapeDataString(url[legacyPrefix.Length..]);
            var file = await storage.TryDownloadDocumentAsync(path, cancellationToken);
            if (file is null)
            {
                results.Add(new BrandingAssetMove(kind, false, $"could not read '{path}' from the documents bucket"));
                return url;
            }

            using var content = new MemoryStream(file.Content);
            var moved = await storage.UploadPublicAsync(path, content, file.ContentType, cancellationToken);
            results.Add(new BrandingAssetMove(kind, true, moved));
            return moved;
        }
    }
}
