using Microsoft.EntityFrameworkCore;
using HvacErp.Application.Common;
using HvacErp.Application.Interfaces;
using HvacErp.Domain.Entities;
using HvacErp.Domain.Enums;

namespace HvacErp.Application.Services;

/// <summary>Default <see cref="Interfaces.IQuoteNumberingService"/>.</summary>
public class QuoteNumberingService(IAppDbContext context) : IQuoteNumberingService
{
    public async Task AssignNumberingAsync(Quote quote, CancellationToken cancellationToken = default)
    {
        var externalProductId = quote.Products
            .FirstOrDefault(p => p.Role == ProductRole.ExternalUnit)?.ProductId;

        // Numbers, versions and revisions run per store: a client quoted in two stores has two series.
        if (quote.StoreId == 0)
        {
            quote.StoreId = context.Tenant.RequireStoreId();
        }

        var existingForClient = await context.Quotes
            .Where(q => q.StoreId == quote.StoreId && q.ClientId == quote.ClientId)
            .Include(q => q.Products)
            .ToListAsync(cancellationToken);

        if (existingForClient.Count == 0)
        {
            var maxNumber = await context.Quotes
                .Where(q => q.StoreId == quote.StoreId)
                .Select(q => (int?)q.Number)
                .MaxAsync(cancellationToken) ?? 0;
            quote.Number = maxNumber + 1;
            quote.Version = 1;
            quote.Revision = 0;
            return;
        }

        var sameExternalUnit = externalProductId is not null
            ? existingForClient.FirstOrDefault(q =>
                q.Products.Any(p => p.Role == ProductRole.ExternalUnit && p.ProductId == externalProductId))
            : null;

        if (sameExternalUnit is not null)
        {
            quote.Number = sameExternalUnit.Number;
            quote.Version = sameExternalUnit.Version;
            quote.Revision = existingForClient
                .Where(q => q.Number == sameExternalUnit.Number && q.Version == sameExternalUnit.Version)
                .Max(q => q.Revision) + 1;
            quote.ParentQuoteId ??= sameExternalUnit.ParentQuoteId ?? sameExternalUnit.Id;
            return;
        }

        var latest = existingForClient
            .OrderByDescending(q => q.Number)
            .ThenByDescending(q => q.Version)
            .First();

        quote.Number = latest.Number;
        quote.Version = existingForClient.Where(q => q.Number == latest.Number).Max(q => q.Version) + 1;
        quote.Revision = 0;
        quote.ParentQuoteId ??= latest.ParentQuoteId ?? latest.Id;
    }
}
