using HvacErp.Application.Dtos;

namespace HvacErp.Application.Interfaces;

/// <summary>Read-only lookup data feeding dropdowns across the Quotes/Repairs/Installations modules.</summary>
public interface ILookupService
{
    /// <summary>Installers with crew numbers.</summary>
    Task<List<InstallerDto>> GetInstallersAsync(CancellationToken cancellationToken = default);
    /// <summary>Sales operators / agents.</summary>
    Task<List<OperatorDto>> GetOperatorsAsync(CancellationToken cancellationToken = default);
    /// <summary>Client marketing sources.</summary>
    Task<List<ClientSourceDto>> GetClientSourcesAsync(CancellationToken cancellationToken = default);
    /// <summary>Product type classification.</summary>
    Task<List<ProductTypeDto>> GetProductTypesAsync(CancellationToken cancellationToken = default);
    /// <summary>Labour / consumables price list.</summary>
    Task<List<ServiceItemDto>> GetServiceItemsAsync(CancellationToken cancellationToken = default);
    /// <summary>Active extra-work types in display order.</summary>
    Task<List<ExtraTypeDto>> GetExtraTypesAsync(CancellationToken cancellationToken = default);
    /// <summary>Active incentive schemes (tax incentives, fees, invoice discounts, line options) in display order.</summary>
    Task<List<IncentiveSchemeDto>> GetIncentiveSchemesAsync(CancellationToken cancellationToken = default);
    /// <summary>Product price lists.</summary>
    Task<List<ProductPriceListDto>> GetProductPriceListsAsync(CancellationToken cancellationToken = default);

    /// <summary>Active VAT schemes with blend shape and multipliers.</summary>
    Task<List<VatOptionDto>> GetVatOptionsAsync(CancellationToken cancellationToken = default);
    /// <summary>Active repair states in legacy ordinal order.</summary>
    Task<List<RepairStatusDto>> GetRepairStatusesAsync(CancellationToken cancellationToken = default);
    /// <summary>Active quote lifecycle milestones.</summary>
    Task<List<LookupItemDto>> GetQuoteStatusesAsync(CancellationToken cancellationToken = default);
    /// <summary>Active ducted-system product lines.</summary>
    Task<List<LookupItemDto>> GetDuctSystemTypesAsync(CancellationToken cancellationToken = default);
    /// <summary>Active quote origin desks.</summary>
    Task<List<LookupItemDto>> GetQuoteDepartmentsAsync(CancellationToken cancellationToken = default);
    /// <summary>Active installation time bands.</summary>
    Task<List<LookupItemDto>> GetDaySlotsAsync(CancellationToken cancellationToken = default);

    /// <summary>Active catalog units, optionally filtered by category (<c>Outdoor</c> / <c>Indoor</c>, see <see cref="Domain.Entities.ProductCategories"/>).</summary>
    Task<List<ProductDto>> GetProductsAsync(string? category = null, CancellationToken cancellationToken = default);
}
