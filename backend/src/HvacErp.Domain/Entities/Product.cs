namespace HvacErp.Domain.Entities;

/// <summary>
/// An HVAC unit catalog entry (external condenser or internal split unit).
/// Whether a given instance is used as an external or internal unit on a quote
/// is decided by <see cref="QuoteProduct.Role"/>, not by a field here — the same
/// catalog doubles as both lists in the legacy app.
/// </summary>
public class Product
{
    public int Id { get; set; }

    public int PriceListId { get; set; }
    public ProductPriceList PriceList { get; set; } = null!;

    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    /// <summary><see cref="ProductCategories.Outdoor"/> or <see cref="ProductCategories.Indoor"/> (which wizard list the unit appears in).</summary>
    public string? Category { get; set; }
    public string? Accessories { get; set; }
    public string? CoolingClass { get; set; }
    public string? HeatingClass { get; set; }
    public string? CoverCode { get; set; }
    public string? Color { get; set; }
    public string? Dimensions { get; set; }
    public string? Ean { get; set; }
    public decimal ExtraPrice { get; set; }
    public string? Filters { get; set; }
    public string? RefWarranty { get; set; }
    public int Stock { get; set; }
    public string? StockNotes { get; set; }
    public string? PhotoId { get; set; }
    public string? Location { get; set; }
    public decimal? MaxBtu { get; set; }
    public decimal? MaxLiters { get; set; }
    public decimal? Power { get; set; }
    public decimal Price { get; set; }
    public decimal? HydronicPrice { get; set; }

    /// <summary>Hydronic unit: priced at <see cref="HydronicPrice"/> and exempt from the refrigerant-gas match.</summary>
    public bool IsHydronic { get; set; }
    public decimal? PriceNotPrepared { get; set; }
    public decimal? PricePrepared { get; set; }
    public string? NoiseLevel { get; set; }
    public string? RefrigerantGas { get; set; }
    public bool Wifi { get; set; }
    public bool Active { get; set; } = true;

    public ICollection<QuoteProduct> QuoteProducts { get; set; } = new List<QuoteProduct>();
}

/// <summary>Stored values of <see cref="Product.Category"/>.</summary>
public static class ProductCategories
{
    /// <summary>Outdoor (condensing) unit.</summary>
    public const string Outdoor = "Outdoor";

    /// <summary>Indoor unit.</summary>
    public const string Indoor = "Indoor";
}
