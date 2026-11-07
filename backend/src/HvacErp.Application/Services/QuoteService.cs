using Microsoft.EntityFrameworkCore;
using HvacErp.Application.Common;
using HvacErp.Application.Dtos;
using HvacErp.Application.Interfaces;
using HvacErp.Domain.Entities;
using HvacErp.Domain.Enums;

namespace HvacErp.Application.Services;

/// <summary>Default <see cref="Interfaces.IQuoteService"/>: delegates numbering and pricing, owns the lifecycle.</summary>
public class QuoteService(
    IAppDbContext context,
    IQuoteNumberingService numbering,
    IQuotePricingService pricing,
    ICurrentUser currentUser) : IQuoteService
{
    public async Task<List<QuoteListItemDto>> GetListAsync(int? clientId = null, CancellationToken cancellationToken = default)
    {
        var query = context.Quotes.Include(q => q.Client).AsQueryable();
        if (clientId is not null)
        {
            query = query.Where(q => q.ClientId == clientId);
        }

        var quotes = await query
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync(cancellationToken);

        return quotes.Select(q => q.ToListItemDto()).ToList();
    }

    public async Task<QuoteDetailDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var quote = await LoadDetailedAsync(id, cancellationToken);
        return quote.ToDetailDto();
    }

    public async Task<QuoteDetailDto> CreateAsync(UpsertQuoteRequest request, CancellationToken cancellationToken = default)
    {
        _ = await context.Clients.FindAsync([request.ClientId], cancellationToken)
            ?? throw new NotFoundException(nameof(Client), request.ClientId);

        var quote = new Quote { ClientId = request.ClientId };
        await ApplyRequestAsync(quote, request, cancellationToken);

        await numbering.AssignNumberingAsync(quote, cancellationToken);
        await pricing.PriceAsync(quote, cancellationToken);

        context.Quotes.Add(quote);
        await context.SaveChangesAsync(cancellationToken);

        var reloaded = await LoadDetailedAsync(quote.Id, cancellationToken);
        return reloaded.ToDetailDto();
    }

    public async Task<QuoteDetailDto> UpdateAsync(int id, UpsertQuoteRequest request, CancellationToken cancellationToken = default)
    {
        var quote = await context.Quotes
            .Include(q => q.Products)
            .Include(q => q.DuctedUnits).ThenInclude(d => d.Outlets)
            .Include(q => q.ServiceItems)
            .Include(q => q.Extras)
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Quote), id);

        if (quote.CurrentStatus == QuoteStatusCodes.Confirmed)
        {
            throw new BusinessRuleException("A confirmed quote can no longer be edited — create a new revision instead.");
        }

        context.QuoteProducts.RemoveRange(quote.Products);
        context.DuctedUnitOutlets.RemoveRange(quote.DuctedUnits.SelectMany(d => d.Outlets));
        context.DuctedUnits.RemoveRange(quote.DuctedUnits);
        context.QuoteServiceItems.RemoveRange(quote.ServiceItems);
        context.QuoteExtras.RemoveRange(quote.Extras);
        quote.Products.Clear();
        quote.DuctedUnits.Clear();
        quote.ServiceItems.Clear();
        quote.Extras.Clear();

        await ApplyRequestAsync(quote, request, cancellationToken);
        await pricing.PriceAsync(quote, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        var reloaded = await LoadDetailedAsync(quote.Id, cancellationToken);
        return reloaded.ToDetailDto();
    }

    public async Task MarkSurveyDoneAsync(int id, CancellationToken cancellationToken = default)
    {
        var quote = await GetTrackedAsync(id, cancellationToken);
        AppendStatusEvent(quote, QuoteStatusCodes.SurveyDone, notes: null);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task RecallAsync(int id, RecallQuoteRequest request, CancellationToken cancellationToken = default)
    {
        var quote = await GetTrackedAsync(id, cancellationToken);
        AppendStatusEvent(quote, QuoteStatusCodes.Recalled, request.Notes);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task ConfirmAsync(int id, CancellationToken cancellationToken = default)
    {
        currentUser.EnsureInRole("confirm a quote", UserRole.Sales, UserRole.Admin);
        var quote = await GetTrackedAsync(id, cancellationToken);
        AppendStatusEvent(quote, QuoteStatusCodes.Confirmed, notes: null);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Toggles the "Sconto in Fattura" acceptance flag. Separate from <see cref="UpdateAsync"/> so it
    /// can still be flipped once the quote is locked (legacy toggled <c>PP_CONFERMATO_SF</c> independently).
    /// </summary>
    public async Task ToggleSfConfirmationAsync(int id, CancellationToken cancellationToken = default)
    {
        currentUser.EnsureInRole("change the invoice-discount confirmation", UserRole.Sales, UserRole.Admin);
        var quote = await GetTrackedAsync(id, cancellationToken);
        if (!quote.SfRequested)
        {
            throw new BusinessRuleException("This quote hasn't requested an invoice discount ('Sconto in Fattura').");
        }

        quote.SfConfirmed = !quote.SfConfirmed;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var quote = await GetTrackedAsync(id, cancellationToken);
        context.Quotes.Remove(quote);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Appends a lifecycle event and moves the denormalized <see cref="Quote.CurrentStatus"/> forward.
    /// Confirmed is terminal: it never regresses to Survey/Recalled.
    /// </summary>
    private void AppendStatusEvent(Quote quote, string status, string? notes)
    {
        quote.StatusEvents.Add(new QuoteStatusEvent
        {
            Status = status,
            OccurredAt = DateTime.UtcNow,
            Notes = notes,
            ChangedBy = currentUser.Name
        });

        if (quote.CurrentStatus != QuoteStatusCodes.Confirmed)
        {
            quote.CurrentStatus = status;
        }
    }

    private async Task<Quote> GetTrackedAsync(int id, CancellationToken cancellationToken) =>
        await context.Quotes
            .Include(q => q.StatusEvents)
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Quote), id);

    private async Task<Quote> LoadDetailedAsync(int id, CancellationToken cancellationToken) =>
        await context.Quotes
            .Include(q => q.Client)
            .Include(q => q.Operator)
            .Include(q => q.Products).ThenInclude(p => p.Product)
            .Include(q => q.DuctedUnits).ThenInclude(d => d.Outlets)
            .Include(q => q.ServiceItems).ThenInclude(s => s.ServiceItem)
            .Include(q => q.Extras).ThenInclude(e => e.ExtraType)
            .Include(q => q.StatusEvents)
            .Include(q => q.Attachments)
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Quote), id);

    private async Task ApplyRequestAsync(Quote quote, UpsertQuoteRequest request, CancellationToken cancellationToken)
    {
        quote.OperatorId = request.OperatorId;
        quote.ParentQuoteId = request.ParentQuoteId;
        quote.Department = request.Department;
        quote.Predisposed = request.Predisposed;
        quote.Vat = request.Vat;
        quote.ManualDiscount = request.ManualDiscount;
        quote.EneaEligible = request.EneaEligible;
        quote.EneaPrice = request.EneaPrice;
        quote.RaeeEligible = request.RaeeEligible;
        quote.RaeePrice = request.RaeePrice;
        quote.SfRequested = request.SfRequested;
        quote.SfConfirmed = request.SfConfirmed;
        quote.InstallationAddress = string.IsNullOrWhiteSpace(request.InstallationAddress) ? null : request.InstallationAddress.Trim();
        quote.InstallationCity = string.IsNullOrWhiteSpace(request.InstallationCity) ? null : request.InstallationCity.Trim();
        quote.InstallationPositionNotes = request.InstallationPositionNotes;
        quote.FloorPlanSketchJson = FloorPlanSketchValidator.Validate(request.FloorPlanSketch);

        foreach (var p in request.Products)
        {
            _ = await context.Products.FindAsync([p.ProductId], cancellationToken)
                ?? throw new NotFoundException(nameof(Product), p.ProductId);

            quote.Products.Add(new QuoteProduct
            {
                ProductId = p.ProductId,
                Role = p.Role,
                Quantity = p.Quantity,
                Warranty = p.Warranty
            });
        }

        foreach (var d in request.DuctedUnits)
        {
            var ducted = new DuctedUnit
            {
                CloudQuantity = d.CloudQuantity,
                PlenumQuantity = d.PlenumQuantity,
                HasFlexibleHose = d.HasFlexibleHose,
                FlexibleHoseQuantity = d.FlexibleHoseQuantity,
                HasRigidHose = d.HasRigidHose,
                RigidHoseQuantity = d.RigidHoseQuantity,
                RemoteQuantity = d.RemoteQuantity
            };

            var position = 1;
            foreach (var o in d.Outlets)
            {
                ducted.Outlets.Add(new DuctedUnitOutlet
                {
                    Position = o.Position > 0 ? o.Position : position,
                    SystemType = o.SystemType,
                    VentCount = o.VentCount
                });
                position++;
            }

            quote.DuctedUnits.Add(ducted);
        }

        foreach (var s in request.ServiceItems)
        {
            var serviceItem = await context.ServiceItems.FindAsync([s.ServiceItemId], cancellationToken)
                ?? throw new NotFoundException(nameof(ServiceItem), s.ServiceItemId);

            quote.ServiceItems.Add(new QuoteServiceItem
            {
                ServiceItemId = s.ServiceItemId,
                Quantity = s.Quantity,
                UnitPrice = serviceItem.Price
            });
        }

        foreach (var e in request.Extras)
        {
            var extraType = await context.ExtraTypes.FindAsync([e.ExtraTypeId], cancellationToken)
                ?? throw new NotFoundException(nameof(ExtraType), e.ExtraTypeId);

            quote.Extras.Add(new QuoteExtra
            {
                ExtraTypeId = extraType.Id,
                Quantity = extraType.QuantityBased ? e.Quantity : 1m,
                UnitPrice = extraType.DefaultPrice
            });
        }
    }
}
