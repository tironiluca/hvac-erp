using HvacErp.Application.Pricing;
using Microsoft.EntityFrameworkCore;
using HvacErp.Application.Pdf;
using HvacErp.Application.Services;
using HvacErp.Domain.Entities;
using HvacErp.Domain.Enums;

namespace HvacErp.Infrastructure.Persistence.Seed;

/// <summary>
/// Idempotent dev/test fixture data for the lookup tables the UI needs to be usable
/// before the real Magazzino/Offerte import tooling exists. Each table is seeded only
/// if empty, so re-running against a database that already has real data is a no-op.
/// Triggered via `dotnet run -- seed` (see Program.cs).
/// </summary>
public static class DevDataSeeder
{
    /// <summary>Seeds lookup / reference data; safe to run repeatedly.</summary>
    public static async Task SeedAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        await SeedLookupTablesAsync(context, cancellationToken);
        await SeedInstallersAsync(context, cancellationToken);
        await SeedOperatorsAsync(context, cancellationToken);
        await SeedClientSourcesAsync(context, cancellationToken);
        await SeedProductTypesAsync(context, cancellationToken);
        await SeedServiceItemsAsync(context, cancellationToken);
        await SeedExtraTypesAsync(context, cancellationToken);
        await SeedIncentiveSchemesAsync(context, cancellationToken);
        var priceLists = await SeedPriceListsAsync(context, cancellationToken);
        await SeedDiscountTiersAsync(context, priceLists, cancellationToken);
        await SeedProductsAsync(context, priceLists, cancellationToken);
        await SeedPortalBrandingAsync(context, cancellationToken);
        await SeedPdfTemplatesAsync(context, cancellationToken);
    }

    /// <summary>
    /// What a newly created tenant cannot work without: the coded service items and extra types the
    /// pricing engine reads, the default incentive schemes and a PDF template per document type.
    /// No sample catalogue, clients or staff — those are the company's own data. The context must
    /// already work for the tenant; safe to run again.
    /// </summary>
    public static async Task SeedTenantDefaultsAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        await SeedServiceItemsAsync(context, cancellationToken);
        await SeedExtraTypesAsync(context, cancellationToken);
        await SeedIncentiveSchemesAsync(context, cancellationToken);
        await SeedPdfTemplatesAsync(context, cancellationToken);
    }

    /// <summary>
    /// The built-in PDF layout template for each document type (quote / repair / installation),
    /// seeded active + <c>IsSystemDefault</c>. Its definition is <see cref="PdfTemplateDefaults"/>,
    /// which reproduces the pre-Phase-10 hardcoded layout. Inserted per type only when that type has
    /// no template yet, so an admin's later edits (or extra templates) are never disturbed. The
    /// migration seeds the same rows; this keeps re-seeding authoritative.
    /// </summary>
    private static async Task SeedPdfTemplatesAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        var existingTypes = await context.PdfTemplates.Select(t => t.DocumentType).Distinct().ToListAsync(cancellationToken);

        foreach (var documentType in Enum.GetValues<PdfDocumentType>())
        {
            if (existingTypes.Contains(documentType))
            {
                continue;
            }

            context.PdfTemplates.Add(new PdfTemplate
            {
                DocumentType = documentType,
                Name = PdfTemplateDefaults.DefaultName(documentType),
                IsActive = true,
                IsSystemDefault = true,
                DefinitionJson = PdfJson.Serialize(PdfTemplateDefaults.Default(documentType)),
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The single <c>PortalBranding</c> row, seeded to the current compile-time default
    /// values so a fresh database themes identically to the SCSS fallback. Inserted only when the
    /// table is empty; an admin's later in-app edits are never overwritten. The migration seeds the
    /// same row, so this only matters when re-seeding a DB created before the table existed.
    /// </summary>
    private static async Task SeedPortalBrandingAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        if (await context.PortalBrandings.AnyAsync(cancellationToken))
        {
            return;
        }

        context.PortalBrandings.Add(new PortalBranding
        {
            Id = 1,
            PrimarySeed = "#2B6CB0",
            TertiarySeed = "#DD6B20",
            NeutralSeed = "#5F6368",
            PortalTitle = PortalBranding.DefaultPortalTitle,
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The Phase 8c enum-replacement lookup tables. Idempotent per <c>Code</c>: a new row added here
    /// backfills on the next run without disturbing metadata (labels, rates, colours) an operator may
    /// have edited. The migration seeds the same rows so a fresh DB satisfies the FK constraints; this
    /// keeps the seeder authoritative for re-seeding and for any code added after the migration ran.
    /// </summary>
    private static async Task SeedLookupTablesAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        await UpsertByCodeAsync(context, context.VatOptions, new[]
        {
            new VatOption { Code = "Standard22", Label = "Standard VAT 22%", SortOrder = 10, BlendShape = "Split", MachinesMultiplier = 1.22m, InstallationMultiplier = 2.20m, PdfLabel = "VAT included (22%)" },
            new VatOption { Code = "Reduced10", Label = "Reduced VAT 10%", SortOrder = 20, BlendShape = "Flat", MachinesMultiplier = 1.10m, InstallationMultiplier = 1.10m, PdfLabel = "Reduced VAT (10%)" },
            new VatOption { Code = "Exempt", Label = "VAT exempt", SortOrder = 30, BlendShape = "None", MachinesMultiplier = 1m, InstallationMultiplier = 1m, PdfLabel = "VAT excluded" },
        }, cancellationToken);

        await UpsertByCodeAsync(context, context.QuoteDepartments, new[]
        {
            new QuoteDepartment { Code = "Store", Label = "Store", SortOrder = 10 },
            new QuoteDepartment { Code = "Field", Label = "Field", SortOrder = 20 },
        }, cancellationToken);

        await UpsertByCodeAsync(context, context.QuoteStatuses, new[]
        {
            new QuoteStatus { Code = "Draft", Label = "Draft", SortOrder = 10 },
            new QuoteStatus { Code = "SurveyDone", Label = "Survey done", SortOrder = 20 },
            new QuoteStatus { Code = "Recalled", Label = "Called back", SortOrder = 30 },
            new QuoteStatus { Code = "Confirmed", Label = "Confirmed", SortOrder = 40, IsTerminal = true },
        }, cancellationToken);

        await UpsertByCodeAsync(context, context.DuctSystemTypes, new[]
        {
            new DuctSystemType { Code = "Standard", Label = "Standard", SortOrder = 10 },
            new DuctSystemType { Code = "Airzone", Label = "Airzone", SortOrder = 20 },
        }, cancellationToken);

        await UpsertByCodeAsync(context, context.DaySlots, new[]
        {
            new DaySlot { Code = "Morning", Label = "Morning", SortOrder = 10 },
            new DaySlot { Code = "Afternoon", Label = "Afternoon", SortOrder = 20 },
            new DaySlot { Code = "FullDay", Label = "Full day", SortOrder = 30 },
        }, cancellationToken);

        // The 10 states confirmed in Phase 8a, at their legacy RipRev.Stato ordinals.
        await UpsertByCodeAsync(context, context.RepairStatuses, new[]
        {
            new RepairStatus { Code = "New", Label = "New", SortOrder = 10, LegacyOrdinal = 0 },
            new RepairStatus { Code = "Urgent", Label = "Urgent", SortOrder = 20, LegacyOrdinal = 1, Color = "#C62828" },
            new RepairStatus { Code = "Scheduled", Label = "Scheduled", Description = "Assigned, work not started yet", SortOrder = 30, LegacyOrdinal = 2 },
            new RepairStatus { Code = "InProgress", Label = "In progress", SortOrder = 40, LegacyOrdinal = 3 },
            new RepairStatus { Code = "AwaitingPayment", Label = "Awaiting payment", SortOrder = 50, LegacyOrdinal = 4 },
            new RepairStatus { Code = "PendingOffSeason", Label = "On hold until the season", SortOrder = 60, LegacyOrdinal = 5 },
            new RepairStatus { Code = "ClosedByInstallers", Label = "Closed by installers", SortOrder = 70, LegacyOrdinal = 6, IsTerminal = true },
            new RepairStatus { Code = "Closed", Label = "Closed", SortOrder = 80, LegacyOrdinal = 7, IsTerminal = true, Color = "#616161" },
            new RepairStatus { Code = "AssistanceRequested", Label = "Manufacturer support requested", SortOrder = 90, LegacyOrdinal = 8, RequiresDirectAssistance = true, Color = "#F9A825" },
            new RepairStatus { Code = "AssistanceContacted", Label = "Manufacturer support contacted", SortOrder = 100, LegacyOrdinal = 9, RequiresDirectAssistance = true, Color = "#F9A825" },
        }, cancellationToken);
    }

    private static async Task UpsertByCodeAsync<T>(
        AppDbContext context, DbSet<T> set, IReadOnlyCollection<T> canonical, CancellationToken cancellationToken)
        where T : Domain.Entities.LookupBase
    {
        var existing = await set.Select(x => x.Code).ToListAsync(cancellationToken);
        var missing = canonical.Where(x => !existing.Contains(x.Code)).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        set.AddRange(missing);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedInstallersAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        // Eight crews so the shared calendar's per-crew lanes have real breadth to
        // show. Idempotent per Name: a crew added here backfills on the next run without touching
        // installers an operator may have since edited.
        var required = new[]
        {
            new Installer { Name = "Marco Colombo", Mobile = "+39 335 123 4567", Email = "marco.colombo@example.com", Department = "Installation crew", Crew = 1 },
            new Installer { Name = "Davide Ferrari", Mobile = "+39 347 234 5678", Email = "davide.ferrari@example.com", Department = "Installation crew", Crew = 1 },
            new Installer { Name = "Simone Bianchi", Mobile = "+39 328 345 6789", Email = "simone.bianchi@example.com", Department = "Service crew", Crew = 2 },
            new Installer { Name = "Luca Riva", Mobile = "+39 333 456 7890", Email = "luca.riva@example.com", Department = "Service crew", Crew = 2 },
            new Installer { Name = "Andrea Conti", Mobile = "+39 340 567 8901", Email = "andrea.conti@example.com", Department = "Installation crew", Crew = 3 },
            new Installer { Name = "Paolo Greco", Mobile = "+39 349 678 9012", Email = "paolo.greco@example.com", Department = "Installation crew", Crew = 3 },
            new Installer { Name = "Fabio Martini", Mobile = "+39 331 789 0123", Email = "fabio.martini@example.com", Department = "Installation crew", Crew = 4 },
            new Installer { Name = "Stefano Villa", Mobile = "+39 338 890 1234", Email = "stefano.villa@example.com", Department = "Installation crew", Crew = 4 },
            new Installer { Name = "Matteo Sala", Mobile = "+39 342 901 2345", Email = "matteo.sala@example.com", Department = "Ducted crew", Crew = 5 },
            new Installer { Name = "Nicola Rizzo", Mobile = "+39 346 012 3456", Email = "nicola.rizzo@example.com", Department = "Ducted crew", Crew = 5 },
            new Installer { Name = "Roberto Gallo", Mobile = "+39 334 123 4560", Email = "roberto.gallo@example.com", Department = "Installation crew", Crew = 6 },
            new Installer { Name = "Enrico Fumagalli", Mobile = "+39 348 234 5671", Email = "enrico.fumagalli@example.com", Department = "Installation crew", Crew = 6 },
            new Installer { Name = "Michele Corti", Mobile = "+39 337 345 6782", Email = "michele.corti@example.com", Department = "Service crew", Crew = 7 },
            new Installer { Name = "Giovanni Beretta", Mobile = "+39 341 456 7893", Email = "giovanni.beretta@example.com", Department = "Service crew", Crew = 7 },
            new Installer { Name = "Alessandro Mauri", Mobile = "+39 339 567 8904", Email = "alessandro.mauri@example.com", Department = "Installation crew", Crew = 8 },
            new Installer { Name = "Cristian Redaelli", Mobile = "+39 343 678 9015", Email = "cristian.redaelli@example.com", Department = "Installation crew", Crew = 8 },
        };

        var existingNames = await context.Installers.Select(i => i.Name).ToListAsync(cancellationToken);
        var missing = required.Where(i => !existingNames.Contains(i.Name)).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        context.Installers.AddRange(missing);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedOperatorsAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        if (await context.Operators.AnyAsync(cancellationToken))
        {
            return;
        }

        context.Operators.AddRange(
            new Operator { Code = "MR", Name = "Mario Rossi", Extension = 101, Email = "mario@example.com" },
            new Operator { Code = "AF", Name = "Anna Ferrari", Extension = 102, Email = "anna@example.com" },
            new Operator { Code = "PC", Name = "Paolo Colombo", Extension = 103, Email = "paolo@example.com" }
        );
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedClientSourcesAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        if (await context.ClientSources.AnyAsync(cancellationToken))
        {
            return;
        }

        context.ClientSources.AddRange(
            new ClientSource { Description = "Word of mouth" },
            new ClientSource { Description = "Website" },
            new ClientSource { Description = "Google Ads" }
        );
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedProductTypesAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        if (await context.ProductTypes.AnyAsync(cancellationToken))
        {
            return;
        }

        context.ProductTypes.AddRange(
            new ProductType { Name = "Wall split", Description = "Wall-mounted split air conditioner, single indoor unit" },
            new ProductType { Name = "Multi-split", Description = "Multi-split system, several indoor units on one outdoor unit" },
            new ProductType { Name = "Cassette", Description = "Ceiling cassette indoor unit" },
            new ProductType { Name = "Ducted", Description = "Ducted indoor unit" }
        );
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Two groups of the same table: free-form line items staff attach to a quote ad hoc, and the
    /// coded rows <c>QuotePricingService</c> reads through <see cref="InstallationCostItems"/> (base
    /// installation price per internal-unit count, ducted accessories). Every coded row must exist or
    /// pricing a quote fails. Coded rows are matched by <c>Code</c> and plain rows by <c>Name</c>, so a
    /// re-run backfills new entries without touching names or prices an operator has since edited.
    /// The toggleable "extra work" items live in <see cref="ExtraType"/>, see <c>SeedExtraTypesAsync</c>.
    /// </summary>
    private static async Task SeedServiceItemsAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        var coded = new (string Code, string Name, decimal Price)[]
        {
            (InstallationCostItems.BaseSystemPriceFor(1), "Installation, single split (1 indoor unit)", 350.00m),
            (InstallationCostItems.BaseSystemPriceFor(2), "Installation, dual split (2 indoor units)", 550.00m),
            (InstallationCostItems.BaseSystemPriceFor(3), "Installation, trial split (3 indoor units)", 750.00m),
            (InstallationCostItems.BaseSystemPriceFor(4), "Installation, quad split (4 indoor units)", 950.00m),
            (InstallationCostItems.BaseSystemPriceFor(5), "Installation, penta split (5+ indoor units)", 1150.00m),
            (InstallationCostItems.Plenum, "Ducted plenum", 120.00m),
            (InstallationCostItems.Cloud, "Ducted cloud module", 180.00m),
            (InstallationCostItems.FlexibleHose, "Ducted flexible hose (per metre)", 22.00m),
            (InstallationCostItems.RigidHose, "Ducted rigid duct (per metre)", 28.00m),
            (InstallationCostItems.Remote, "Ducted remote control", 45.00m),
        };
        var plain = new (string Name, decimal Price)[]
        {
            ("Outdoor unit wall bracket", 35.00m),
            ("Extra refrigerant pipe (per metre)", 18.00m),
            ("Packaging and old unit disposal", 25.00m),
        };

        var existingCodes = await context.ServiceItems.Where(s => s.Code != null).Select(s => s.Code!).ToListAsync(cancellationToken);
        var existingNames = await context.ServiceItems.Select(s => s.Name).ToListAsync(cancellationToken);

        var missing = coded.Where(c => !existingCodes.Contains(c.Code))
            .Select(c => new ServiceItem { Code = c.Code, Name = c.Name, Price = c.Price })
            .Concat(plain.Where(p => !existingNames.Contains(p.Name))
                .Select(p => new ServiceItem { Name = p.Name, Price = p.Price }))
            .ToList();

        if (missing.Count == 0)
        {
            return;
        }

        context.ServiceItems.AddRange(missing);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The toggleable "extra work" catalogue the Quotes wizard offers (electrical wiring, scaffolding,
    /// extra wall holes, WiFi module, …). Idempotent per <c>Code</c> so a new extra added here backfills
    /// on the next run without disturbing prices an operator may have since edited.
    /// </summary>
    private static async Task SeedExtraTypesAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        var required = new[]
        {
            new ExtraType { Code = "ELECTRICAL_WIRING", Label = "Electrical wiring", DefaultPrice = 80.00m, QuantityBased = false, SortOrder = 10 },
            new ExtraType { Code = "ROOF_BRACKET", Label = "Roof bracket", DefaultPrice = 60.00m, QuantityBased = false, SortOrder = 20 },
            new ExtraType { Code = "SCAFFOLDING", Label = "Scaffolding", DefaultPrice = 150.00m, QuantityBased = false, SortOrder = 30 },
            new ExtraType { Code = "EXTENSION_LADDER", Label = "Aerial ladder", DefaultPrice = 200.00m, QuantityBased = false, SortOrder = 40 },
            new ExtraType { Code = "DRAIN_PUMP", Label = "Condensate pump", DefaultPrice = 90.00m, QuantityBased = false, SortOrder = 50 },
            new ExtraType { Code = "INSULATION_FIXING", Label = "External-insulation fixing", DefaultPrice = 70.00m, QuantityBased = false, SortOrder = 60 },
            new ExtraType { Code = "PIPE_EXTENSION_M", Label = "Pipe extension", Description = "Price per metre", DefaultPrice = 25.00m, QuantityBased = true, SortOrder = 70 },
            new ExtraType { Code = "EXTRA_HOLE_30CM", Label = "Extra 30 cm wall hole", Description = "Price per hole", DefaultPrice = 35.00m, QuantityBased = true, SortOrder = 80 },
            new ExtraType { Code = "PIPE_INVERSION", Label = "Pipe reversal", DefaultPrice = 40.00m, QuantityBased = false, SortOrder = 90 },
            new ExtraType { Code = "EXTRA_TRACE_M", Label = "Extra wall chase", Description = "Price per metre", DefaultPrice = 20.00m, QuantityBased = true, SortOrder = 100 },
            new ExtraType { Code = "WIFI", Label = "WiFi module", DefaultPrice = 50.00m, QuantityBased = false, ExcludedFromVat = true, SortOrder = 110 },
        };

        var existingCodes = await context.ExtraTypes.Select(e => e.Code).ToListAsync(cancellationToken);
        var missing = required.Where(e => !existingCodes.Contains(e.Code)).ToList();

        if (missing.Count == 0)
        {
            return;
        }

        context.ExtraTypes.AddRange(missing);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The default incentive schemes (<see cref="IncentiveSchemeDefaults"/>). Idempotent per <c>Code</c>:
    /// a scheme added there backfills on the next run without touching rates an admin has since edited.
    /// </summary>
    private static async Task SeedIncentiveSchemesAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        var existingCodes = await context.IncentiveSchemes.Select(s => s.Code).ToListAsync(cancellationToken);
        var missing = IncentiveSchemeDefaults.All().Where(s => !existingCodes.Contains(s.Code)).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        context.IncentiveSchemes.AddRange(missing);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task<List<ProductPriceList>> SeedPriceListsAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        if (await context.ProductPriceLists.AnyAsync(cancellationToken))
        {
            return await context.ProductPriceLists.ToListAsync(cancellationToken);
        }

        var priceLists = new List<ProductPriceList>
        {
            new()
            {
                Description = "Standard price list 2026",
                Vat = 22.00m,
                Markup = 35.00m,
                InitialDiscount = 0.00m,
                DiscountTiers =
                {
                    new PriceListDiscountTier { TierOrder = 1, Percentage = 10.00m },
                    new PriceListDiscountTier { TierOrder = 2, Percentage = 5.00m }
                },
                TransportFee = 50.00m,
                WasteDisposalFee = 15.00m,
                DepositFee = 100.00m,
                CoversExternalCode = true
            },
            new()
            {
                Description = "Summer promotion 2026",
                Vat = 22.00m,
                Markup = 30.00m,
                InitialDiscount = 5.00m,
                DiscountTiers =
                {
                    new PriceListDiscountTier { TierOrder = 1, Percentage = 10.00m }
                },
                TransportFee = 0.00m,
                WasteDisposalFee = 15.00m,
                DepositFee = 50.00m,
                CoversExternalCode = false
            }
        };

        context.ProductPriceLists.AddRange(priceLists);
        await context.SaveChangesAsync(cancellationToken);
        return priceLists;
    }

    /// <summary>
    /// Backfills a default two-rung discount ladder onto any price list that has no tiers — covers
    /// price lists that were seeded before the <see cref="PriceListDiscountTier"/> table existed
    /// (a fresh <c>SeedPriceListsAsync</c> already attaches them inline).
    /// </summary>
    private static async Task SeedDiscountTiersAsync(AppDbContext context, List<ProductPriceList> priceLists, CancellationToken cancellationToken)
    {
        var withoutTiers = await context.ProductPriceLists
            .Where(p => !p.DiscountTiers.Any())
            .ToListAsync(cancellationToken);

        if (withoutTiers.Count == 0)
        {
            return;
        }

        foreach (var priceList in withoutTiers)
        {
            context.PriceListDiscountTiers.AddRange(
                new PriceListDiscountTier { PriceListId = priceList.Id, TierOrder = 1, Percentage = 10.00m },
                new PriceListDiscountTier { PriceListId = priceList.Id, TierOrder = 2, Percentage = 5.00m });
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedProductsAsync(AppDbContext context, List<ProductPriceList> priceLists, CancellationToken cancellationToken)
    {
        if (await context.Products.AnyAsync(cancellationToken))
        {
            return;
        }

        var standard = priceLists[0];
        var promo = priceLists[1];

        context.Products.AddRange(
            new Product
            {
                PriceList = standard, Brand = "Daikin", Model = "Sensira FTXF25C", Category = ProductCategories.Indoor,
                CoolingClass = "A++", HeatingClass = "A+", MaxBtu = 9000, Power = 2.5m, Price = 450.00m,
                Stock = 12, RefrigerantGas = "R32", Wifi = false, Active = true
            },
            new Product
            {
                PriceList = standard, Brand = "Daikin", Model = "Sensira RXF25C", Category = ProductCategories.Outdoor,
                MaxBtu = 9000, Power = 2.5m, Price = 380.00m, Stock = 10, RefrigerantGas = "R32", Active = true
            },
            new Product
            {
                PriceList = promo, Brand = "Mitsubishi Electric", Model = "MSZ-AP25VG", Category = ProductCategories.Indoor,
                CoolingClass = "A+++", HeatingClass = "A++", MaxBtu = 9000, Power = 2.5m, Price = 520.00m,
                Stock = 8, RefrigerantGas = "R32", Wifi = true, Active = true
            },
            new Product
            {
                PriceList = promo, Brand = "Mitsubishi Electric", Model = "MUZ-AP25VG", Category = ProductCategories.Outdoor,
                MaxBtu = 9000, Power = 2.5m, Price = 420.00m, Stock = 8, RefrigerantGas = "R32", Active = true
            }
        );
        await context.SaveChangesAsync(cancellationToken);
    }
}
