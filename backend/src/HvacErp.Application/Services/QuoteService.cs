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
            .Include(q => q.Incentives)
            .Include(q => q.Products).ThenInclude(p => p.Incentives)
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
        // Customer acceptances are not part of the edit form: they survive a save.
        var previousIncentives = quote.Incentives.ToDictionary(i => i.IncentiveSchemeId);
        context.QuoteProductIncentives.RemoveRange(quote.Products.SelectMany(p => p.Incentives));
        context.QuoteIncentives.RemoveRange(quote.Incentives);
        quote.Products.Clear();
        quote.DuctedUnits.Clear();
        quote.ServiceItems.Clear();
        quote.Extras.Clear();
        quote.Incentives.Clear();

        await ApplyRequestAsync(quote, request, cancellationToken, previousIncentives);
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
    /// Toggles the customer's acceptance of an incentive that requires it (an invoice discount).
    /// Separate from <see cref="UpdateAsync"/> so it can still be flipped once the quote is locked
    /// (legacy toggled <c>PP_CONFERMATO_SF</c> independently).
    /// </summary>
    public async Task ToggleIncentiveConfirmationAsync(int id, int schemeId, CancellationToken cancellationToken = default)
    {
        currentUser.EnsureInRole("change an incentive confirmation", UserRole.Sales, UserRole.Admin);
        var incentive = await context.QuoteIncentives
            .Include(i => i.IncentiveScheme)
            .FirstOrDefaultAsync(i => i.QuoteId == id && i.IncentiveSchemeId == schemeId, cancellationToken)
            ?? throw new NotFoundException(nameof(QuoteIncentive), $"{id}/{schemeId}");

        if (!incentive.IncentiveScheme.RequiresConfirmation)
        {
            throw new BusinessRuleException($"'{incentive.IncentiveScheme.Label}' does not need the customer's confirmation.");
        }

        incentive.Confirmed = !incentive.Confirmed;
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
            .Include(q => q.Products).ThenInclude(p => p.Incentives).ThenInclude(i => i.IncentiveScheme)
            .Include(q => q.DuctedUnits).ThenInclude(d => d.Outlets)
            .Include(q => q.ServiceItems).ThenInclude(s => s.ServiceItem)
            .Include(q => q.Extras).ThenInclude(e => e.ExtraType)
            .Include(q => q.Incentives).ThenInclude(i => i.IncentiveScheme)
            .Include(q => q.StatusEvents)
            .Include(q => q.Attachments)
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Quote), id);

    private async Task ApplyRequestAsync(
        Quote quote,
        UpsertQuoteRequest request,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<int, QuoteIncentive>? previousIncentives = null)
    {
        previousIncentives ??= new Dictionary<int, QuoteIncentive>();
        var schemes = await LoadRequestedSchemesAsync(request, previousIncentives, cancellationToken);

        quote.OperatorId = request.OperatorId;
        quote.ParentQuoteId = request.ParentQuoteId;
        quote.Department = request.Department;
        quote.Predisposed = request.Predisposed;
        quote.Vat = request.Vat;
        quote.ManualDiscount = request.ManualDiscount;
        quote.InstallationAddress = string.IsNullOrWhiteSpace(request.InstallationAddress) ? null : request.InstallationAddress.Trim();
        quote.InstallationCity = string.IsNullOrWhiteSpace(request.InstallationCity) ? null : request.InstallationCity.Trim();
        quote.InstallationPositionNotes = request.InstallationPositionNotes;
        quote.FloorPlanSketchJson = FloorPlanSketchValidator.Validate(request.FloorPlanSketch);

        foreach (var p in request.Products)
        {
            _ = await context.Products.FindAsync([p.ProductId], cancellationToken)
                ?? throw new NotFoundException(nameof(Product), p.ProductId);

            var line = new QuoteProduct
            {
                ProductId = p.ProductId,
                Role = p.Role,
                Quantity = p.Quantity,
            };
            foreach (var input in (p.Incentives ?? []).DistinctBy(i => i.IncentiveSchemeId))
            {
                var scheme = schemes[input.IncentiveSchemeId];
                if (scheme.Kind != IncentiveKinds.LineAmount)
                {
                    throw new BusinessRuleException($"'{scheme.Label}' applies to the whole quote, not to a product line.");
                }

                var (defaultMode, defaultValue) = QuotePricingService.DefaultLineAmount(scheme);
                var mode = input.Mode ?? defaultMode;
                if (!LineAmountModes.All.Contains(mode))
                {
                    throw new BusinessRuleException(
                        $"'{mode}' is not a way to price '{scheme.Label}'. Use {string.Join(" or ", LineAmountModes.All)}.");
                }
                // A mode switched on the line without a value takes the scheme's default for that mode.
                var value = input.Value
                    ?? (mode == defaultMode ? defaultValue : mode == LineAmountModes.Fixed ? scheme.DefaultAmount ?? 0m : scheme.Percent ?? 0m);
                if (value < 0)
                {
                    throw new BusinessRuleException($"The value for '{scheme.Label}' cannot be negative.");
                }

                line.Incentives.Add(new QuoteProductIncentive
                {
                    IncentiveSchemeId = scheme.Id,
                    IncentiveScheme = scheme,
                    Mode = mode,
                    Value = value,
                });
            }

            quote.Products.Add(line);
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

        foreach (var input in (request.Incentives ?? []).DistinctBy(i => i.IncentiveSchemeId))
        {
            var scheme = schemes[input.IncentiveSchemeId];
            if (scheme.Kind == IncentiveKinds.LineAmount)
            {
                throw new BusinessRuleException($"'{scheme.Label}' applies to a product line, not to the whole quote.");
            }
            if (input.Amount is < 0)
            {
                throw new BusinessRuleException($"The amount for '{scheme.Label}' cannot be negative.");
            }

            quote.Incentives.Add(new QuoteIncentive
            {
                IncentiveSchemeId = scheme.Id,
                IncentiveScheme = scheme,
                // QuoteAmount: entered or default. Computed kinds are filled in by the pricing engine.
                Amount = scheme.Kind == IncentiveKinds.QuoteAmount ? input.Amount ?? scheme.DefaultAmount : null,
                Confirmed = previousIncentives.TryGetValue(scheme.Id, out var previous) && previous.Confirmed,
            });
        }

        if (quote.Incentives.Count(i => i.IncentiveScheme.Kind == IncentiveKinds.InvoiceDiscount) > 1)
        {
            throw new BusinessRuleException("A quote can carry only one invoice-discount scheme.");
        }
    }

    /// <summary>
    /// Loads every scheme the request refers to. A scheme an admin has since deactivated can stay on
    /// a quote that already had it, but cannot be newly added.
    /// </summary>
    private async Task<Dictionary<int, IncentiveScheme>> LoadRequestedSchemesAsync(
        UpsertQuoteRequest request,
        IReadOnlyDictionary<int, QuoteIncentive> previousQuoteIncentives,
        CancellationToken cancellationToken)
    {
        var ids = (request.Incentives ?? []).Select(i => i.IncentiveSchemeId)
            .Concat(request.Products.SelectMany(p => (p.Incentives ?? []).Select(i => i.IncentiveSchemeId)))
            .Distinct()
            .ToList();
        var schemes = await context.IncentiveSchemes
            .Where(s => ids.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        foreach (var id in ids)
        {
            if (!schemes.TryGetValue(id, out var scheme))
            {
                throw new NotFoundException(nameof(IncentiveScheme), id);
            }
            if (!scheme.Active && !previousQuoteIncentives.ContainsKey(id))
            {
                throw new BusinessRuleException($"'{scheme.Label}' is no longer offered.");
            }
        }

        return schemes;
    }
}
