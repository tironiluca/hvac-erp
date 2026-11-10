using Microsoft.EntityFrameworkCore;
using HvacErp.Application.Common;
using HvacErp.Application.Dtos;
using HvacErp.Application.Erp;
using HvacErp.Application.Interfaces;
using HvacErp.Domain.Entities;
using HvacErp.Domain.Enums;

namespace HvacErp.Application.Services;

/// <summary>Default <see cref="Interfaces.IQuoteService"/>: delegates numbering and pricing, owns the lifecycle.</summary>
public class QuoteService(
    IAppDbContext context,
    IQuoteNumberingService numbering,
    IQuotePricingService pricing,
    ICurrentUser currentUser,
    IErpExportService? erp = null,
    IFileStorageService? storage = null) : IQuoteService
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

        // The store is decided first: without one there is nothing to price or number against.
        var quote = new Quote { ClientId = request.ClientId, StoreId = context.Tenant.RequireStoreId() };
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
        if (erp is not null)
        {
            // Same transaction as the confirmation: the order cannot be lost if the ERP is down.
            await erp.EnqueueAsync(id, quote.StatusEvents.Last().OccurredAt, cancellationToken);
        }
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
            // Through the quote, so its store filter applies (child rows are filtered by tenant only).
            .FirstOrDefaultAsync(i => i.Quote.Id == id && i.IncentiveSchemeId == schemeId, cancellationToken)
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

        // A quote other records depend on is not deleted from under them.
        if (await context.ScheduledInstallations.AnyAsync(s => s.QuoteId == id, cancellationToken))
        {
            throw new BusinessRuleException("This quote has a scheduled installation — remove it from the calendar first.");
        }

        // Deleting would cascade to the export row and lose the only trace of an order the ERP holds.
        if (await context.ErpOrderExports.AnyAsync(e => e.QuoteId == id && e.Status == ErpExportStatuses.Sent, cancellationToken))
        {
            throw new BusinessRuleException("This quote's order is already in the ERP, so it can no longer be deleted.");
        }

        // The quote's files go with it, after the rows: a leftover file is clutter, a leftover row
        // pointing at a deleted file is a broken link.
        var files = await context.QuoteAttachments.Where(a => a.QuoteId == id).Select(a => a.FilePath).ToListAsync(cancellationToken);
        if (quote.PdfPath is not null)
        {
            files.Add(quote.PdfPath);
        }

        context.Quotes.Remove(quote);
        await context.SaveChangesAsync(cancellationToken);

        if (storage is not null)
        {
            foreach (var path in files)
            {
                await storage.TryDeleteAsync(path, cancellationToken);
            }
        }
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

            quote.Products.Add(new QuoteProduct
            {
                ProductId = p.ProductId,
                Role = p.Role,
                Quantity = p.Quantity,
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

        foreach (var input in (request.Incentives ?? []).DistinctBy(i => i.IncentiveSchemeId))
        {
            var scheme = schemes[input.IncentiveSchemeId];
            (string? mode, decimal? value) = scheme.Kind == IncentiveKinds.QuoteAmount
                ? ResolveAmountInput(scheme, input)
                : ((string?)null, (decimal?)null);

            quote.Incentives.Add(new QuoteIncentive
            {
                IncentiveSchemeId = scheme.Id,
                IncentiveScheme = scheme,
                Mode = mode,
                Value = value,
                // Amounts are filled in by the pricing engine.
                Confirmed = previousIncentives.TryGetValue(scheme.Id, out var previous) && previous.Confirmed,
            });
        }

        if (quote.Incentives.Count(i => i.IncentiveScheme.Kind == IncentiveKinds.InvoiceDiscount) > 1)
        {
            throw new BusinessRuleException("A quote can carry only one invoice-discount scheme.");
        }
    }

    /// <summary>
    /// The mode and value of a quote amount: as sent, else the scheme's default. A mode sent without a
    /// value takes the scheme's default for that mode.
    /// </summary>
    private static (string Mode, decimal Value) ResolveAmountInput(IncentiveScheme scheme, QuoteIncentiveInput input)
    {
        var (defaultMode, defaultValue) = QuotePricingService.DefaultQuoteAmount(scheme);
        var mode = input.Mode ?? defaultMode;
        if (!AmountModes.All.Contains(mode))
        {
            throw new BusinessRuleException(
                $"'{mode}' is not a way to price '{scheme.Label}'. Use {string.Join(" or ", AmountModes.All)}.");
        }

        var value = input.Value
                    ?? (mode == defaultMode ? defaultValue : mode == AmountModes.Fixed ? scheme.DefaultAmount ?? 0m : scheme.Percent ?? 0m);
        if (value < 0)
        {
            throw new BusinessRuleException($"The value for '{scheme.Label}' cannot be negative.");
        }

        return (mode, value);
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
