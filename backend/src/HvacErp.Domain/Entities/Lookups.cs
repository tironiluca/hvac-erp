namespace HvacErp.Domain.Entities;

/// <summary>A field installer; <see cref="Crew"/> groups installers into calendar lanes.</summary>
public class Installer : ITenantOwned
{
    public int TenantId { get; set; }

    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Mobile { get; set; }
    public string? Email { get; set; }
    public string? Department { get; set; }
    public int? Crew { get; set; }
}

/// <summary>A sales operator / agent who owns quotes.</summary>
public class Operator : ITenantOwned
{
    public int TenantId { get; set; }

    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? Extension { get; set; }
    public string? Email { get; set; }
}

/// <summary>How a client found the business (marketing source).</summary>
public class ClientSource : ITenantOwned
{
    public int TenantId { get; set; }

    public int Id { get; set; }
    public string Description { get; set; } = string.Empty;
}

/// <summary>Classification of products and service items.</summary>
public class ProductType : ITenantOwned
{
    public int TenantId { get; set; }

    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>Pricing tier/rules applied to one or more products (markup, VAT, discount ladder, fees).</summary>
public class ProductPriceList : ITenantOwned
{
    public int TenantId { get; set; }

    public int Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Vat { get; set; }
    public decimal Markup { get; set; }
    public decimal InitialDiscount { get; set; }
    public decimal TransportFee { get; set; }
    public decimal WasteDisposalFee { get; set; }
    public decimal DepositFee { get; set; }
    public bool CoversExternalCode { get; set; }

    /// <summary>The cascading discount ladder — replaces the legacy fixed <c>Discount1..6</c> columns.</summary>
    public ICollection<PriceListDiscountTier> DiscountTiers { get; set; } = new List<PriceListDiscountTier>();

    public ICollection<Product> Products { get; set; } = new List<Product>();
}

/// <summary>One rung of a price list's cascading discount ladder (applied in <see cref="TierOrder"/> order).</summary>
public class PriceListDiscountTier : ITenantOwned
{
    public int TenantId { get; set; }

    public int Id { get; set; }

    public int PriceListId { get; set; }
    public ProductPriceList PriceList { get; set; } = null!;

    public int TierOrder { get; set; }
    public decimal Percentage { get; set; }
}

/// <summary>
/// A kind of "extra work" a quote can include (electrical wiring, scaffolding, extra wall holes,
/// WiFi module, …). DB-backed so the business can add a new extra without a code change; carries
/// its own default price and the metadata the pricing engine needs.
/// </summary>
public class ExtraType : ITenantOwned
{
    public int TenantId { get; set; }

    public int Id { get; set; }

    /// <summary>Stable machine key (e.g. <c>ELECTRICAL_WIRING</c>) — safe for code to branch on.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Human label shown in the UI and on the PDF (Italian).</summary>
    public string Label { get; set; } = string.Empty;
    public string? Description { get; set; }

    public decimal DefaultPrice { get; set; }

    /// <summary>True when the line quantity is meaningful (meters, holes); false for on/off extras (always qty 1).</summary>
    public bool QuantityBased { get; set; }

    /// <summary>True for extras billed flat, outside the VAT-blended total (legacy WiFi module behaviour).</summary>
    public bool ExcludedFromVat { get; set; }

    public int SortOrder { get; set; }
    public bool Active { get; set; } = true;
}

/// <summary>A line item from the labor/consumables price list (e.g. wall bracket, extra pipe meter, duct grille).</summary>
public class ServiceItem : ITenantOwned
{
    public int TenantId { get; set; }

    public int Id { get; set; }

    /// <summary>
    /// Stable key for the rows the pricing engine reads (base installation and ducted accessories,
    /// see <c>InstallationCostItems</c>); null for ordinary line items. <see cref="Name"/> is the
    /// display text and can be renamed or translated freely.
    /// </summary>
    public string? Code { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }

    public int? ProductTypeId { get; set; }
    public ProductType? ProductType { get; set; }
}
