namespace HvacErp.Domain.Entities;

/// <summary>
/// F-Gas (EU refrigerant-handling regulation) compliance record. The legacy app had a menu
/// entry for this but no implementation behind it — this is designed fresh, not migrated.
/// </summary>
public class FGasRecord : IStoreOwned
{
    public int TenantId { get; set; }
    public int StoreId { get; set; }

    public int Id { get; set; }

    public int? QuoteId { get; set; }
    public Quote? Quote { get; set; }

    public int? RepairId { get; set; }
    public Repair? Repair { get; set; }

    public string GasType { get; set; } = string.Empty;
    public decimal KilogramsOfGas { get; set; }
    public bool IsRefill { get; set; }
    public bool HasExtraCharge { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
