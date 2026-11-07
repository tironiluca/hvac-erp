using HvacErp.Application.Pricing;
using HvacErp.Domain.Entities;
using HvacErp.Domain.Enums;

namespace HvacErp.Application.Pdf;

/// <summary>
/// Deterministic in-memory entity graphs used only by the template-editor <b>preview</b> endpoint,
/// so a preview never touches the database and always exercises every section (two units, a ducted
/// outlet, extras, service items, an invoice-discount block, …). Shapes match what the real
/// <c>*PdfService</c> classes eager-load.
/// </summary>
public static class PdfSampleData
{
    public const string VatLabel = "VAT included (22%)";
    public const string SlotLabel = "MORNING";

    /// <summary>A tiny hand-drawn sample — two sheets (room outline + a diagonal, then an L-shape) — so the multi-sheet floor-plan section renders in the preview.</summary>
    private const string SampleFloorPlanSketch =
        "{\"version\":1,\"width\":1000,\"height\":667,\"sheets\":[" +
        "{\"strokes\":[" +
        "{\"color\":\"#1a1c1d\",\"width\":5,\"opacity\":1,\"points\":[[130,110],[860,110],[860,540],[130,540],[130,110]]}," +
        "{\"color\":\"#c0392b\",\"width\":3,\"opacity\":1,\"points\":[[130,110],[860,540]]}," +
        "{\"color\":\"#f1c40f\",\"width\":26,\"opacity\":0.35,\"points\":[[220,320],[560,320]]}" +
        "]}," +
        "{\"strokes\":[" +
        "{\"color\":\"#1a1c1d\",\"width\":5,\"opacity\":1,\"points\":[[150,120],[700,120],[700,360],[880,360],[880,540],[150,540],[150,120]]}" +
        "]}" +
        "]}";

    private static IncentiveScheme Scheme(string code) => IncentiveSchemeDefaults.All().Single(s => s.Code == code);

    private static Client SampleClient() => new()
    {
        Id = 1,
        Code = "CP0042",
        FullName = "Alex Morgan",
        Address = "12 Station Road",
        City = "Springfield",
        Phone = "+39 035 123456",
        Mobile = "+39 347 1234567",
        Email = "alex.morgan@example.com",
    };

    /// <summary>A realistic in-memory quote for template previews.</summary>
    public static Quote Quote()
    {
        var client = SampleClient();
        var priceList = new ProductPriceList { Id = 1, Description = "Standard price list" };

        Product Unit(string brand, string model, string cool, string heat) => new()
        {
            Id = Random.Shared.Next(1000, 9999),
            PriceList = priceList,
            Brand = brand,
            Model = model,
            CoolingClass = cool,
            HeatingClass = heat,
            RefrigerantGas = "R32",
        };

        return new Quote
        {
            Id = 1,
            Number = 128,
            Version = 1,
            Revision = 0,
            CreatedAt = new DateTime(2026, 6, 14, 0, 0, 0, DateTimeKind.Utc),
            Client = client,
            ClientId = client.Id,
            Operator = new Operator { Id = 1, Code = "MR", Name = "Sam Taylor" },
            Department = QuoteDepartmentCodes.Store,
            Predisposed = true,
            Vat = VatCodes.Standard22,
            MachinesPrice = 2400m,
            InstallationPrice = 1200m,
            FinalPrice = 4260m,
            Incentives =
            [
                new QuoteIncentive { IncentiveScheme = Scheme(IncentiveSchemeDefaults.EnergyTaxDeduction), Amount = 150m },
                new QuoteIncentive { IncentiveScheme = Scheme(IncentiveSchemeDefaults.WeeeTakeBack), Amount = 30m },
                new QuoteIncentive { IncentiveScheme = Scheme(IncentiveSchemeDefaults.InvoiceDiscount50), Amount = 5550m },
            ],
            InstallationPositionNotes = "Outdoor unit on the courtyard balcony; indoor units in the living room and main bedroom.",
            FloorPlanSketchJson = SampleFloorPlanSketch,
            Products =
            [
                new QuoteProduct
                {
                    Product = Unit("Daikin", "Sensira RXF25C", "-", "-"),
                    Role = ProductRole.ExternalUnit, Quantity = 1, Price = 900m, DiscountedPrice = 780m,
                    Incentives = [new QuoteProductIncentive { IncentiveScheme = Scheme(IncentiveSchemeDefaults.AllInclusiveWarranty), Amount = 72m }],
                },
                new QuoteProduct
                {
                    Product = Unit("Daikin", "Sensira FTXF25C", "A++", "A+"),
                    Role = ProductRole.InternalUnit, Quantity = 2, Price = 520m, DiscountedPrice = 470m,
                    Incentives = [new QuoteProductIncentive { IncentiveScheme = Scheme(IncentiveSchemeDefaults.AllInclusiveWarranty), Amount = 83.2m }],
                },
            ],
            DuctedUnits =
            [
                new DuctedUnit
                {
                    PlenumQuantity = 1, RemoteQuantity = 1, CloudQuantity = 0,
                    Outlets =
                    [
                        new DuctedUnitOutlet { Position = 1, SystemType = DuctSystemTypeCodes.Standard, VentCount = 3 },
                    ],
                },
            ],
            Extras =
            [
                new QuoteExtra
                {
                    ExtraType = new ExtraType { Code = "SCAFFOLDING", Label = "Scaffolding", SortOrder = 30 },
                    Quantity = 1, UnitPrice = 150m,
                },
                new QuoteExtra
                {
                    ExtraType = new ExtraType { Code = "PIPE_EXTENSION_M", Label = "Pipe extension", QuantityBased = true, SortOrder = 70 },
                    Quantity = 4, UnitPrice = 25m,
                },
            ],
            ServiceItems =
            [
                new QuoteServiceItem
                {
                    ServiceItem = new ServiceItem { Name = "Outdoor unit wall bracket" }, Quantity = 1, UnitPrice = 35m,
                },
                new QuoteServiceItem
                {
                    ServiceItem = new ServiceItem { Name = "Extra refrigerant pipe (per metre)" }, Quantity = 3, UnitPrice = 18m,
                },
            ],
        };
    }

    /// <summary>A realistic in-memory repair for template previews.</summary>
    public static Repair Repair()
    {
        var client = SampleClient();
        return new Repair
        {
            Id = 57,
            Type = RepairType.Repair,
            Status = RepairStatusCodes.Scheduled,
            Client = client,
            ClientId = client.Id,
            Brand = "Mitsubishi Electric",
            Description = "The living-room unit does not cool and shows error E6. Check the refrigerant charge and temperature sensor.",
            Urgent = true,
            Predisposed = false,
            AllInclusive = false,
            UnitCount = 1,
            SplitCount = 2,
            Rounding = 0m,
            InsertedAt = new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc),
            InstallationDate = new DateTime(2026, 7, 9, 0, 0, 0, DateTimeKind.Utc),
            FloorPlanSketchJson = SampleFloorPlanSketch,
            ScheduleEntries =
            [
                new RepairScheduleEntry
                {
                    Date = new DateTime(2026, 7, 9, 0, 0, 0, DateTimeKind.Utc),
                    Installer = new Installer { Name = "Chris Walker" },
                    Notes = "First visit: diagnosis",
                },
            ],
            ServiceItems =
            [
                new RepairServiceItem { ServiceItem = new ServiceItem { Name = "Ricarica gas R32 (kg)" }, Quantity = 0.8m, UnitPrice = 45m },
                new RepairServiceItem { ServiceItem = new ServiceItem { Name = "Technician labour (hour)" }, Quantity = 2m, UnitPrice = 40m },
            ],
        };
    }

    /// <summary>A realistic in-memory installation for template previews.</summary>
    public static ScheduledInstallation Installation()
    {
        var quote = Quote();
        return new ScheduledInstallation
        {
            Id = 33,
            Quote = quote,
            QuoteId = quote.Id,
            Date = new DateTime(2026, 7, 21, 0, 0, 0, DateTimeKind.Utc),
            Slot = DaySlotCodes.Morning,
            Notes = "Bring a mobile scaffold. Access from the inner courtyard, buzzer 'Morgan'.",
            FloorPlanSketchJson = SampleFloorPlanSketch,
            Installers =
            [
                new InstallationInstaller { Installer = new Installer { Name = "Chris Walker" } },
                new InstallationInstaller { Installer = new Installer { Name = "Jordan Lee" } },
            ],
        };
    }
}
