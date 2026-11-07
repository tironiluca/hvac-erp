using Microsoft.EntityFrameworkCore;
using HvacErp.Application.Common;
using HvacErp.Application.Dtos;
using HvacErp.Application.Interfaces;

namespace HvacErp.Application.Services;

/// <summary>Default <see cref="Interfaces.ILookupService"/> over EF Core (read-only).</summary>
public class LookupService(IAppDbContext context) : ILookupService
{
    public async Task<List<InstallerDto>> GetInstallersAsync(CancellationToken cancellationToken = default) =>
        await context.Installers.OrderBy(i => i.Name)
            .Select(i => new InstallerDto(i.Id, i.Name, i.Mobile, i.Email, i.Department, i.Crew))
            .ToListAsync(cancellationToken);

    public async Task<List<OperatorDto>> GetOperatorsAsync(CancellationToken cancellationToken = default) =>
        await context.Operators.OrderBy(o => o.Name)
            .Select(o => new OperatorDto(o.Id, o.Code, o.Name, o.Extension, o.Email))
            .ToListAsync(cancellationToken);

    public async Task<List<ClientSourceDto>> GetClientSourcesAsync(CancellationToken cancellationToken = default) =>
        await context.ClientSources.OrderBy(s => s.Description)
            .Select(s => new ClientSourceDto(s.Id, s.Description))
            .ToListAsync(cancellationToken);

    public async Task<List<ProductTypeDto>> GetProductTypesAsync(CancellationToken cancellationToken = default) =>
        await context.ProductTypes.OrderBy(t => t.Name)
            .Select(t => new ProductTypeDto(t.Id, t.Name, t.Description))
            .ToListAsync(cancellationToken);

    public async Task<List<ServiceItemDto>> GetServiceItemsAsync(CancellationToken cancellationToken = default) =>
        await context.ServiceItems.OrderBy(s => s.Name)
            .Select(s => new ServiceItemDto(s.Id, s.Code, s.Name, s.Description, s.Price, s.ProductTypeId))
            .ToListAsync(cancellationToken);

    public async Task<List<ExtraTypeDto>> GetExtraTypesAsync(CancellationToken cancellationToken = default) =>
        await context.ExtraTypes.Where(e => e.Active).OrderBy(e => e.SortOrder).ThenBy(e => e.Label)
            .Select(e => new ExtraTypeDto(
                e.Id, e.Code, e.Label, e.Description, e.DefaultPrice, e.QuantityBased, e.ExcludedFromVat, e.SortOrder))
            .ToListAsync(cancellationToken);

    public async Task<List<ProductPriceListDto>> GetProductPriceListsAsync(CancellationToken cancellationToken = default) =>
        await context.ProductPriceLists.OrderBy(p => p.Description)
            .Select(p => new ProductPriceListDto(p.Id, p.Description, p.Vat, p.Markup, p.CoversExternalCode))
            .ToListAsync(cancellationToken);

    public async Task<List<VatOptionDto>> GetVatOptionsAsync(CancellationToken cancellationToken = default) =>
        await context.VatOptions.Where(v => v.Active).OrderBy(v => v.SortOrder)
            .Select(v => new VatOptionDto(
                v.Code, v.Label, v.Color, v.SortOrder, v.BlendShape, v.MachinesMultiplier, v.InstallationMultiplier, v.PdfLabel))
            .ToListAsync(cancellationToken);

    public async Task<List<RepairStatusDto>> GetRepairStatusesAsync(CancellationToken cancellationToken = default) =>
        await context.RepairStatuses.Where(s => s.Active).OrderBy(s => s.SortOrder)
            .Select(s => new RepairStatusDto(
                s.Code, s.Label, s.Description, s.Color, s.SortOrder, s.LegacyOrdinal, s.IsTerminal, s.RequiresDirectAssistance))
            .ToListAsync(cancellationToken);

    public async Task<List<LookupItemDto>> GetQuoteStatusesAsync(CancellationToken cancellationToken = default) =>
        await context.QuoteStatuses.Where(s => s.Active).OrderBy(s => s.SortOrder)
            .Select(s => new LookupItemDto(s.Code, s.Label, s.Description, s.Color, s.SortOrder))
            .ToListAsync(cancellationToken);

    public async Task<List<LookupItemDto>> GetDuctSystemTypesAsync(CancellationToken cancellationToken = default) =>
        await context.DuctSystemTypes.Where(s => s.Active).OrderBy(s => s.SortOrder)
            .Select(s => new LookupItemDto(s.Code, s.Label, s.Description, s.Color, s.SortOrder))
            .ToListAsync(cancellationToken);

    public async Task<List<LookupItemDto>> GetQuoteDepartmentsAsync(CancellationToken cancellationToken = default) =>
        await context.QuoteDepartments.Where(s => s.Active).OrderBy(s => s.SortOrder)
            .Select(s => new LookupItemDto(s.Code, s.Label, s.Description, s.Color, s.SortOrder))
            .ToListAsync(cancellationToken);

    public async Task<List<LookupItemDto>> GetDaySlotsAsync(CancellationToken cancellationToken = default) =>
        await context.DaySlots.Where(s => s.Active).OrderBy(s => s.SortOrder)
            .Select(s => new LookupItemDto(s.Code, s.Label, s.Description, s.Color, s.SortOrder))
            .ToListAsync(cancellationToken);

    public async Task<List<IncentiveSchemeDto>> GetIncentiveSchemesAsync(CancellationToken cancellationToken = default) =>
        await context.IncentiveSchemes.Where(s => s.Active).OrderBy(s => s.SortOrder).ThenBy(s => s.Label)
            .Select(s => new IncentiveSchemeDto(
                s.Id, s.Code, s.Label, s.Description, s.Kind, s.DefaultAmount, s.Percent, s.AddToPrice,
                s.IncludeInInvoiceDiscountBasis, s.RequiresVat, s.RequiresConfirmation,
                s.Threshold, s.RateBelowThreshold, s.RateAboveThreshold, s.RoundingStep, s.SortOrder))
            .ToListAsync(cancellationToken);

    public async Task<List<ProductDto>> GetProductsAsync(string? category = null, CancellationToken cancellationToken = default)
    {
        var query = context.Products.Where(p => p.Active);
        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(p => p.Category == category);
        }

        return await query.OrderBy(p => p.Brand).ThenBy(p => p.Model)
            .Select(p => new ProductDto(
                p.Id, p.PriceListId, p.Brand, p.Model, p.Category, p.Ean, p.MaxBtu, p.Power,
                p.Price, p.HydronicPrice, p.IsHydronic, p.PriceNotPrepared, p.PricePrepared, p.RefrigerantGas, p.Wifi, p.Active, p.Stock,
                p.PriceList.DiscountTiers
                    .OrderBy(t => t.TierOrder)
                    .Select(t => new DiscountTierDto(t.TierOrder, t.Percentage))
                    .ToList()))
            .ToListAsync(cancellationToken);
    }
}
