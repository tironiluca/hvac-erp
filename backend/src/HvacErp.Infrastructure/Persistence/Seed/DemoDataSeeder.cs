using HvacErp.Application.Pricing;
using Microsoft.EntityFrameworkCore;
using HvacErp.Application.Services;
using HvacErp.Domain.Entities;
using HvacErp.Domain.Enums;

namespace HvacErp.Infrastructure.Persistence.Seed;

/// <summary>
/// Bulk, realistic demo/test data — a large interlinked dataset (clients, an expanded product
/// catalogue, quotes across every status with line items / extras / ducted systems / status
/// history, repairs &amp; revisions across all 10 states, scheduled installations, F-Gas records)
/// so the UI can be exercised at volume before the real import tooling exists.
///
/// Triggered via <c>dotnet run -- seed-demo</c> (see Program.cs). Runs <see cref="DevDataSeeder"/>
/// first, so lookups / installers / operators / price lists / base service items always exist.
///
/// Idempotent by a sentinel: every generated client's <c>Code</c> is <c>DEMO####</c>, and the
/// whole run is skipped if any such client already exists. To re-generate, delete the demo rows
/// first (they cascade): <c>DELETE FROM clients WHERE code LIKE 'DEMO%'</c> removes quotes,
/// repairs, installations and F-Gas records with them — except <c>scheduled_installations</c>
/// (quote FK is <c>RESTRICT</c>), so drop those first.
///
/// The RNG is fixed-seeded, so a given code version produces the same shape every time.
/// </summary>
public static class DemoDataSeeder
{
    // Volume knobs — tune here.
    private const int ClientCount = 130;
    private const int QuoteCount = 220;
    private const int RepairCount = 160;
    private const int FGasCount = 110;
    private const double VersionedQuoteFraction = 0.10; // share of confirmed quotes that get a v2
    private const int CalendarFillRadiusDays = 42; // dense installation band on either side of "today"

    private static readonly Random Rng = new(20260827);

    // Public base URL of the demo PDFs / photos (`<storage>/demo`), set per run from configuration
    // so no deployment's storage host is baked into the seed. A one-shot CLI command, like Rng.
    private static string DemoAssetBaseUrl = "https://example.com/demo";

    // The incentive schemes by code, loaded per run (seeded by DevDataSeeder); a one-shot CLI command, like Rng.
    private static Dictionary<string, IncentiveScheme> Schemes = [];

    /// <summary>Adds the demo dataset; safe to run repeatedly.</summary>
    /// <param name="context">The database to seed.</param>
    /// <param name="assetBaseUrl">
    /// Public URL the demo attachments are linked under (e.g. the storage bucket's <c>demo/</c> folder);
    /// null keeps a neutral placeholder.
    /// </param>
    /// <param name="cancellationToken">Cancels the seed.</param>
    public static async Task SeedAsync(
        AppDbContext context, string? assetBaseUrl = null, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(assetBaseUrl))
        {
            DemoAssetBaseUrl = assetBaseUrl.TrimEnd('/');
        }

        await DevDataSeeder.SeedAsync(context, cancellationToken);

        if (await context.Clients.AnyAsync(c => c.Code.StartsWith("DEMO"), cancellationToken))
        {
            Console.WriteLine("DemoDataSeeder: DEMO* clients already present — skipping (idempotent).");
            return;
        }

        var priceLists = await context.ProductPriceLists.OrderBy(p => p.Id).ToListAsync(cancellationToken);
        var operators = await context.Operators.OrderBy(o => o.Id).ToListAsync(cancellationToken);
        var installers = await context.Installers.OrderBy(i => i.Id).ToListAsync(cancellationToken);
        var sources = await context.ClientSources.OrderBy(s => s.Id).ToListAsync(cancellationToken);

        var products = await SeedCatalogAsync(context, priceLists, cancellationToken);
        var internals = products.Where(p => p.Category == ProductCategories.Indoor).ToList();
        var monoExternals = products.Where(p => p.Category == ProductCategories.Outdoor && !IsMultiExternal(p)).ToList();
        var multiExternals = products.Where(p => p.Category == ProductCategories.Outdoor && IsMultiExternal(p)).ToList();

        var serviceItems = await context.ServiceItems.ToListAsync(cancellationToken);
        var serviceByCode = serviceItems
            .Where(s => s.Code != null)
            .ToDictionary(s => s.Code!);
        // Ad-hoc line items: every uncoded row (the coded ones are priced by the engine).
        var lineServiceItems = serviceItems.Where(s => s.Code == null).ToList();
        var extraTypes = await context.ExtraTypes.Where(e => e.Active).ToListAsync(cancellationToken);
        Schemes = await context.IncentiveSchemes.ToDictionaryAsync(s => s.Code, cancellationToken);

        var clients = BuildClients(sources);
        context.Clients.AddRange(clients);
        await context.SaveChangesAsync(cancellationToken);
        Console.WriteLine($"DemoDataSeeder: {clients.Count} clients.");

        // "Cliente amico" referral links — a fifth of clients point back at an earlier one.
        for (var i = 5; i < clients.Count; i++)
        {
            if (Chance(0.20))
            {
                clients[i].ReferredByClientId = clients[Rng.Next(i)].Id;
            }
        }
        await context.SaveChangesAsync(cancellationToken);

        var nextNumber = (await context.Quotes.MaxAsync(q => (int?)q.Number, cancellationToken) ?? 0) + 1;

        var quotes = new List<Quote>(QuoteCount);
        for (var i = 0; i < QuoteCount; i++)
        {
            var quote = BuildQuote(
                clients[Rng.Next(clients.Count)], operators, internals, monoExternals, multiExternals,
                serviceByCode, lineServiceItems, extraTypes, nextNumber + i);
            quotes.Add(quote);
        }
        context.Quotes.AddRange(quotes);
        await context.SaveChangesAsync(cancellationToken);
        Console.WriteLine($"DemoDataSeeder: {quotes.Count} quotes " +
            $"({quotes.Count(q => q.CurrentStatus == QuoteStatusCodes.Confirmed)} confirmed).");

        var versioned = BuildVersionedRevisions(quotes, operators);
        context.Quotes.AddRange(versioned);
        await context.SaveChangesAsync(cancellationToken);
        Console.WriteLine($"DemoDataSeeder: {versioned.Count} follow-up quote revisions.");

        var confirmed = quotes.Concat(versioned)
            .Where(q => q.CurrentStatus == QuoteStatusCodes.Confirmed)
            .ToList();

        var repairs = new List<Repair>(RepairCount);
        for (var i = 0; i < RepairCount; i++)
        {
            var client = clients[Rng.Next(clients.Count)];
            var linkable = confirmed.Where(q => q.ClientId == client.Id).ToList();
            var originQuote = linkable.Count > 0 && Chance(0.35) ? linkable[Rng.Next(linkable.Count)] : null;
            repairs.Add(BuildRepair(client, originQuote, serviceItems, installers, operators));
        }
        context.Repairs.AddRange(repairs);
        await context.SaveChangesAsync(cancellationToken);
        Console.WriteLine($"DemoDataSeeder: {repairs.Count} repairs/revisions.");

        var installations = BuildInstallations(confirmed, installers);
        installations.AddRange(BuildCalendarFill(confirmed, installers));
        context.ScheduledInstallations.AddRange(installations);
        await context.SaveChangesAsync(cancellationToken);
        Console.WriteLine($"DemoDataSeeder: {installations.Count} scheduled installations.");

        var fgas = BuildFGasRecords(quotes.Concat(versioned).ToList(), repairs);
        context.FGasRecords.AddRange(fgas);
        await context.SaveChangesAsync(cancellationToken);
        Console.WriteLine($"DemoDataSeeder: {fgas.Count} F-Gas records.");

        Console.WriteLine("DemoDataSeeder: done.");
    }

    // ---------------------------------------------------------------- catalogue

    private static bool IsMultiExternal(Product p) =>
        p.Model.Contains("MXM") || p.Model.Contains("MXZ") || p.Model.Contains("MU4") || p.Model.Contains("MU5")
        || p.Model.Contains("2AMW") || p.Model.Contains("3AMW") || p.Model.Contains("4AMW") || p.Model.Contains("5AMW");

    private static async Task<List<Product>> SeedCatalogAsync(
        AppDbContext context, List<ProductPriceList> priceLists, CancellationToken cancellationToken)
    {
        if (await context.Products.CountAsync(cancellationToken) >= 30)
        {
            return await context.Products.ToListAsync(cancellationToken);
        }

        // (brand, model, btu, kW, netPrice, coolClass, heatClass, wifi, gas)
        var indoor = new (string Brand, string Model, int Btu, decimal Kw, decimal Price, string Cool, string Heat, bool Wifi, string Gas)[]
        {
            ("Daikin", "Perfera FTXM20R", 7000, 2.0m, 610m, "A+++", "A+++", true, "R32"),
            ("Daikin", "Perfera FTXM25R", 9000, 2.5m, 640m, "A+++", "A+++", true, "R32"),
            ("Daikin", "Perfera FTXM35R", 12000, 3.5m, 720m, "A+++", "A+++", true, "R32"),
            ("Daikin", "Perfera FTXM42R", 15000, 4.2m, 890m, "A++", "A++", true, "R32"),
            ("Daikin", "Perfera FTXM50R", 18000, 5.0m, 1010m, "A++", "A++", true, "R32"),
            ("Daikin", "Stylish FTXA25CW", 9000, 2.5m, 780m, "A+++", "A+++", true, "R32"),
            ("Daikin", "Stylish FTXA35CW", 12000, 3.5m, 860m, "A+++", "A+++", true, "R32"),
            ("Daikin", "Emura FTXJ35AW", 12000, 3.5m, 990m, "A+++", "A+++", true, "R32"),
            ("Daikin", "Sensira FTXF25E", 9000, 2.5m, 470m, "A++", "A+", false, "R32"),
            ("Daikin", "Sensira FTXF35E", 12000, 3.5m, 540m, "A++", "A+", false, "R32"),
            ("Mitsubishi Electric", "MSZ-AP20VG", 7000, 2.0m, 520m, "A+++", "A++", true, "R32"),
            ("Mitsubishi Electric", "MSZ-AP25VG", 9000, 2.5m, 560m, "A+++", "A++", true, "R32"),
            ("Mitsubishi Electric", "MSZ-AP35VG", 12000, 3.5m, 640m, "A+++", "A++", true, "R32"),
            ("Mitsubishi Electric", "MSZ-AP42VG", 15000, 4.2m, 820m, "A++", "A+", true, "R32"),
            ("Mitsubishi Electric", "MSZ-AP50VG", 18000, 5.0m, 940m, "A++", "A+", true, "R32"),
            ("Mitsubishi Electric", "MSZ-LN25VG2W", 9000, 2.5m, 900m, "A+++", "A+++", true, "R32"),
            ("Mitsubishi Electric", "MSZ-LN35VG2W", 12000, 3.5m, 990m, "A+++", "A+++", true, "R32"),
            ("Mitsubishi Electric", "MSZ-EF25VGKW", 9000, 2.5m, 760m, "A+++", "A++", true, "R32"),
            ("Toshiba", "Seiya RAS-B10E2KVG-E", 7000, 2.0m, 420m, "A++", "A+", false, "R32"),
            ("Toshiba", "Seiya RAS-B13E2KVG-E", 12000, 3.3m, 470m, "A++", "A+", false, "R32"),
            ("Toshiba", "Shorai Edge RAS-B10G3KVSG-E", 7000, 2.0m, 650m, "A+++", "A+++", true, "R32"),
            ("Toshiba", "Shorai Edge RAS-B13G3KVSG-E", 12000, 3.3m, 720m, "A+++", "A++", true, "R32"),
            ("Samsung", "WindFree Elite AR09TXCAAWKNEU", 9000, 2.5m, 590m, "A+++", "A+++", true, "R32"),
            ("Samsung", "WindFree Elite AR12TXCAAWKNEU", 12000, 3.5m, 670m, "A+++", "A++", true, "R32"),
            ("Hisense", "Energy Pro X CD25YR05G", 9000, 2.6m, 480m, "A+++", "A++", true, "R32"),
            ("Hisense", "Energy Pro X CD35YR05G", 12000, 3.5m, 560m, "A+++", "A++", true, "R32"),
            ("Panasonic", "Etherea CS-Z25ZKEW", 9000, 2.5m, 700m, "A+++", "A+++", true, "R32"),
            ("Panasonic", "Etherea CS-Z35ZKEW", 12000, 3.5m, 790m, "A+++", "A+++", true, "R32"),
            ("Daikin", "Stylish FTXA25BS Silver", 9000, 2.5m, 820m, "A+++", "A+++", true, "R32"),
            ("Mitsubishi Electric", "MSZ-BT25VGK", 9000, 2.5m, 540m, "A++", "A+", true, "R32"),
        };

        // (brand, model, maxBtu, kW, netPrice, gas) — single-split outdoor
        var monoOut = new (string Brand, string Model, int Btu, decimal Kw, decimal Price, string Gas)[]
        {
            ("Daikin", "RXM20R", 7000, 2.0m, 430m, "R32"),
            ("Daikin", "RXM25R", 9000, 2.5m, 450m, "R32"),
            ("Daikin", "RXM35R", 12000, 3.5m, 520m, "R32"),
            ("Daikin", "RXM50R", 18000, 5.0m, 690m, "R32"),
            ("Mitsubishi Electric", "MUZ-AP25VG", 9000, 2.5m, 460m, "R32"),
            ("Mitsubishi Electric", "MUZ-AP35VG", 12000, 3.5m, 540m, "R32"),
            ("Mitsubishi Electric", "MUZ-AP50VG", 18000, 5.0m, 720m, "R32"),
            ("Toshiba", "RAS-10E2AVG-E", 9000, 2.5m, 360m, "R32"),
            ("Panasonic", "CU-Z25ZKE", 9000, 2.5m, 500m, "R32"),
        };

        // (brand, model, maxBtu, kW, netPrice, gas, ports)
        var multiOut = new (string Brand, string Model, int Btu, decimal Kw, decimal Price, string Gas, int Ports)[]
        {
            ("Daikin", "2MXM40A", 14000, 4.0m, 980m, "R32", 2),
            ("Daikin", "2MXM50A", 18000, 5.0m, 1120m, "R32", 2),
            ("Daikin", "3MXM52A", 18000, 5.2m, 1350m, "R32", 3),
            ("Daikin", "3MXM68A", 24000, 6.8m, 1560m, "R32", 3),
            ("Daikin", "4MXM80A", 27000, 8.0m, 1990m, "R32", 4),
            ("Daikin", "5MXM90A", 30000, 9.0m, 2450m, "R32", 5),
            ("Mitsubishi Electric", "MXZ-2F42VF", 14000, 4.2m, 1010m, "R32", 2),
            ("Mitsubishi Electric", "MXZ-3F54VF", 18000, 5.4m, 1380m, "R32", 3),
            ("Mitsubishi Electric", "MXZ-4F72VF", 24000, 7.2m, 1890m, "R32", 4),
            ("Mitsubishi Electric", "MXZ-5F102VF", 34000, 10.2m, 2600m, "R32", 5),
        };

        var list = new List<Product>();
        var i = 0;
        foreach (var u in indoor)
        {
            list.Add(new Product
            {
                PriceList = priceLists[i++ % priceLists.Count],
                Brand = u.Brand, Model = u.Model, Category = ProductCategories.Indoor,
                CoolingClass = u.Cool, HeatingClass = u.Heat, MaxBtu = u.Btu, Power = u.Kw,
                Price = u.Price, PricePrepared = u.Price, PriceNotPrepared = u.Price + 120m,
                Stock = Rng.Next(0, 26), RefrigerantGas = u.Gas, Wifi = u.Wifi,
                NoiseLevel = $"{Rng.Next(19, 27)} dB(A)",
                Dimensions = "798x295x189 mm",
                Active = Chance(0.94),
            });
        }
        foreach (var u in monoOut)
        {
            list.Add(new Product
            {
                PriceList = priceLists[i++ % priceLists.Count],
                Brand = u.Brand, Model = u.Model, Category = ProductCategories.Outdoor,
                MaxBtu = u.Btu, Power = u.Kw, Price = u.Price, Stock = Rng.Next(0, 18),
                RefrigerantGas = u.Gas, NoiseLevel = $"{Rng.Next(46, 55)} dB(A)", Active = true,
            });
        }
        foreach (var u in multiOut)
        {
            list.Add(new Product
            {
                PriceList = priceLists[i++ % priceLists.Count],
                Brand = u.Brand, Model = u.Model, Category = ProductCategories.Outdoor,
                MaxBtu = u.Btu, Power = u.Kw, Price = u.Price, Stock = Rng.Next(0, 10),
                RefrigerantGas = u.Gas, Accessories = $"{u.Ports} attacchi",
                NoiseLevel = $"{Rng.Next(48, 58)} dB(A)", Active = true,
            });
        }

        context.Products.AddRange(list);
        await context.SaveChangesAsync(cancellationToken);
        Console.WriteLine($"DemoDataSeeder: +{list.Count} catalogue products.");

        return await context.Products.ToListAsync(cancellationToken);
    }

    // ---------------------------------------------------------------- clients

    private static readonly string[] FirstNames =
    {
        "Marco", "Luca", "Giulia", "Francesca", "Alessandro", "Matteo", "Chiara", "Sara", "Andrea", "Davide",
        "Federico", "Elena", "Martina", "Simone", "Paolo", "Giorgia", "Roberto", "Stefano", "Laura", "Valentina",
        "Antonio", "Giovanni", "Silvia", "Cristina", "Fabio", "Nicola", "Monica", "Barbara", "Enrico", "Michele",
    };

    private static readonly string[] LastNames =
    {
        "Rossi", "Ferrari", "Russo", "Bianchi", "Colombo", "Ricci", "Marino", "Greco", "Bruno", "Gallo",
        "Conti", "De Luca", "Mancini", "Costa", "Giordano", "Rizzo", "Lombardi", "Moretti", "Barbieri", "Fontana",
        "Santoro", "Mariani", "Rinaldi", "Caruso", "Ferrara", "Galli", "Martini", "Leone", "Longo", "Gentile",
        "Villa", "Brambilla", "Cattaneo", "Fumagalli", "Sala", "Crippa", "Mauri", "Redaelli", "Corti", "Beretta",
    };

    private static readonly (string City, string Cap)[] Cities =
    {
        ("Monza", "20900"), ("Lissone", "20851"), ("Seregno", "20831"), ("Desio", "20832"), ("Cesano Maderno", "20811"),
        ("Muggiò", "20835"), ("Nova Milanese", "20834"), ("Vimercate", "20871"), ("Brugherio", "20861"), ("Carate Brianza", "20841"),
        ("Giussano", "20833"), ("Meda", "20821"), ("Besana in Brianza", "20842"), ("Arcore", "20862"), ("Concorezzo", "20863"),
        ("Milano", "20125"), ("Sesto San Giovanni", "20099"), ("Cinisello Balsamo", "20092"), ("Cologno Monzese", "20093"),
        ("Agrate Brianza", "20864"), ("Villasanta", "20852"), ("Biassono", "20853"), ("Macherio", "20846"), ("Sovico", "20845"),
    };

    private static readonly string[] Streets =
    {
        "Via Roma", "Via Milano", "Via Garibaldi", "Via Manzoni", "Corso Italia", "Via Dante", "Via Vittorio Veneto",
        "Via Cavour", "Via della Repubblica", "Via IV Novembre", "Via Trento", "Via Trieste", "Via Monte Grappa",
        "Via San Martino", "Via delle Industrie", "Via Buonarroti", "Via Leonardo da Vinci", "Via Volta", "Via Fermi",
    };

    private static readonly string[] BusinessSuffix = { "S.r.l.", "S.n.c.", "S.a.s.", "& C. S.r.l.", "S.r.l.s." };
    private static readonly string[] BusinessNoun =
    {
        "Impianti", "Costruzioni", "Immobiliare", "Serramenti", "Ristorazione", "Autofficina", "Studio Dentistico",
        "Farmacia", "Panificio", "Logistica", "Grafica", "Consulenze",
    };

    private static List<Client> BuildClients(List<ClientSource> sources)
    {
        var list = new List<Client>(ClientCount);
        for (var i = 0; i < ClientCount; i++)
        {
            var (city, cap) = Pick(Cities);
            var isBusiness = Chance(0.14);
            string fullName;
            string? taxCode;

            if (isBusiness)
            {
                fullName = $"{Pick(LastNames)} {Pick(BusinessNoun)} {Pick(BusinessSuffix)}";
                taxCode = RandomDigits(11); // P.IVA
            }
            else
            {
                var first = Pick(FirstNames);
                var last = Pick(LastNames);
                fullName = $"{first} {last}";
                taxCode = Chance(0.45) ? FakeCodiceFiscale(first, last) : null;
            }

            var street = $"{Pick(Streets)} {Rng.Next(1, 180)}";
            var hasEmail = Chance(0.78);
            var slug = fullName.ToLowerInvariant()
                .Replace(" ", ".").Replace("'", "").Replace("&", "").Replace("..", ".").Trim('.');

            list.Add(new Client
            {
                Code = $"DEMO{(i + 1):D4}",
                FullName = fullName,
                Address = street,
                City = $"{cap} {city}",
                Phone = Chance(0.55) ? $"0{Rng.Next(2, 4)} {Rng.Next(200, 999)} {RandomDigits(4)}" : null,
                Mobile = $"3{Rng.Next(20, 99)} {RandomDigits(3)} {RandomDigits(4)}",
                Email = hasEmail ? $"{slug}@{Pick(new[] { "gmail.com", "libero.it", "outlook.it", "pec.it", "tiscali.it" })}" : null,
                TaxCode = taxCode,
                ShippingAddress = Chance(0.12) ? $"{Pick(Streets)} {Rng.Next(1, 120)}" : null,
                ShippingCity = null,
                SourceId = Chance(0.85) ? Pick(sources).Id : null,
            });
        }
        return list;
    }

    // ---------------------------------------------------------------- quotes

    private static readonly string[] PositionNotes =
    {
        "Unità interna in soggiorno sopra la portafinestra; unità esterna sul balcone.",
        "Split in camera matrimoniale, parete a nord. Esterna a terra sul retro.",
        "Due unità: soggiorno e studio. Motore su staffa a parete lato cortile.",
        "Predisposizione già presente dal costruttore, attacchi a 2,4 m.",
        "Esterna sul tetto piano, necessaria autoscala. Percorso tubi ~8 m.",
        "Cucina abitabile, scarico condensa in colonna esistente.",
        "Mansarda: canalizzato con 3 bocchette, plenum in controsoffitto.",
    };

    private static readonly string?[] StatusNotes =
    {
        "Cliente valuta anche la concorrenza.", "Richiamare dopo le ferie.", "In attesa di conferma condominio per l'unità esterna.",
        "Sopralluogo effettuato, misure ok.", "Confermato a voce, atteso bonifico caparra.", "Rivista scontistica su richiesta cliente.",
        null, null, null,
    };

    private static Quote BuildQuote(
        Client client, List<Operator> operators, List<Product> internals,
        List<Product> monoExternals, List<Product> multiExternals,
        Dictionary<string, ServiceItem> serviceByCode, List<ServiceItem> lineServiceItems,
        List<ExtraType> extraTypes, int number)
    {
        var createdAt = RecentUtc(20, 560);
        var internalCount = Weighted((1, 45), (2, 30), (3, 15), (4, 7), (5, 3));
        var vat = Weighted((VatCodes.Standard22, 70), (VatCodes.Reduced10, 25), (VatCodes.Exempt, 5));
        var status = Weighted(
            (QuoteStatusCodes.Draft, 25), (QuoteStatusCodes.SurveyDone, 20),
            (QuoteStatusCodes.Recalled, 15), (QuoteStatusCodes.Confirmed, 40));
        var predisposed = Chance(0.35);

        var quote = new Quote
        {
            Number = number,
            Version = 1,
            Revision = 0,
            ClientId = client.Id,
            OperatorId = Chance(0.9) ? Pick(operators).Id : null,
            Department = Chance(0.6) ? QuoteDepartmentCodes.Store : QuoteDepartmentCodes.Field,
            CreatedAt = createdAt,
            CurrentStatus = status,
            Predisposed = predisposed,
            Vat = vat,
            ManualDiscount = Chance(0.25) ? Round2(Rng.Next(1, 9) * 25m) : 0m,
            InstallationPositionNotes = Chance(0.45) ? Pick(PositionNotes) : null,
        };

        // Units: one external sized to the indoor count + N internals.
        var external = internalCount >= 2 && multiExternals.Count > 0
            ? Pick(multiExternals)
            : Pick(monoExternals);
        quote.Products.Add(NewQuoteProduct(external, ProductRole.ExternalUnit, 1));

        decimal machinesNet = DiscountedLine(external, 1);
        for (var u = 0; u < internalCount; u++)
        {
            var unit = Pick(internals);
            var qty = Chance(0.08) ? 2 : 1;
            quote.Products.Add(NewQuoteProduct(unit, ProductRole.InternalUnit, qty));
            machinesNet += DiscountedLine(unit, qty);
        }

        // Base installation cost item for the number of indoor units.
        var baseCode = InstallationCostItems.BaseSystemPriceFor(internalCount);
        decimal installNet = 0m;
        if (serviceByCode.TryGetValue(baseCode, out var baseItem))
        {
            quote.ServiceItems.Add(new QuoteServiceItem { ServiceItemId = baseItem.Id, Quantity = 1, UnitPrice = baseItem.Price });
            installNet += baseItem.Price;
        }

        foreach (var si in TakeRandom(lineServiceItems, Rng.Next(0, 4)))
        {
            var qty = si.Name.Contains("metre", StringComparison.OrdinalIgnoreCase) ? Rng.Next(2, 10) : 1;
            quote.ServiceItems.Add(new QuoteServiceItem { ServiceItemId = si.Id, Quantity = qty, UnitPrice = si.Price });
            installNet += si.Price * qty;
        }

        foreach (var et in TakeRandom(extraTypes, Rng.Next(0, 4)))
        {
            var qty = et.QuantityBased ? Rng.Next(2, 9) : 1;
            quote.Extras.Add(new QuoteExtra { ExtraTypeId = et.Id, Quantity = qty, UnitPrice = et.DefaultPrice });
            if (!et.ExcludedFromVat)
            {
                installNet += et.DefaultPrice * qty;
            }
        }

        // ~12% of quotes carry a ducted ("canalizzato") system.
        if (Chance(0.12))
        {
            var outletCount = Rng.Next(1, 4);
            var ducted = new DuctedUnit
            {
                PlenumQuantity = Rng.Next(0, 3),
                CloudQuantity = Chance(0.4) ? 1 : 0,
                HasFlexibleHose = Chance(0.7),
                FlexibleHoseQuantity = Rng.Next(4, 16),
                HasRigidHose = Chance(0.3),
                RigidHoseQuantity = Rng.Next(2, 8),
                RemoteQuantity = Rng.Next(0, 2),
            };
            for (var p = 1; p <= outletCount; p++)
            {
                ducted.Outlets.Add(new DuctedUnitOutlet
                {
                    Position = p,
                    SystemType = Chance(0.75) ? DuctSystemTypeCodes.Standard : DuctSystemTypeCodes.Airzone,
                    VentCount = Rng.Next(2, 7),
                });
            }
            quote.DuctedUnits.Add(ducted);
            installNet += 400m + outletCount * 250m;
        }

        // Pricing — plausible, internally consistent (the real engine recomputes on edit).
        var grossMult = vat switch
        {
            VatCodes.Standard22 => 1.22m,
            VatCodes.Reduced10 => 1.10m,
            _ => 1.00m,
        };
        var machinesGross = Round2(machinesNet * grossMult);
        var installGross = Round2(installNet * grossMult);
        var raw = machinesGross + installGross - quote.ManualDiscount;
        var finalRounded = Math.Ceiling(raw / 10m) * 10m;

        quote.MachinesPrice = machinesGross;
        quote.InstallationPrice = installGross;
        quote.Rounding = Round2(finalRounded - raw);
        quote.FinalPrice = finalRounded;

        decimal discountBasisExtra = 0m;
        if (Chance(0.30) && Schemes.TryGetValue(IncentiveSchemeDefaults.EnergyTaxDeduction, out var deduction))
        {
            var fee = Rng.Next(1, 4) * 50m;
            quote.Incentives.Add(new QuoteIncentive { IncentiveSchemeId = deduction.Id, Amount = fee });
            if (deduction.IncludeInInvoiceDiscountBasis)
            {
                discountBasisExtra += fee;
            }
        }
        if (Chance(0.20) && Schemes.TryGetValue(IncentiveSchemeDefaults.WeeeTakeBack, out var weee))
        {
            quote.Incentives.Add(new QuoteIncentive { IncentiveSchemeId = weee.Id, Amount = Round2(Rng.Next(1, 4) * 15m) });
        }
        if (vat != VatCodes.Exempt && Chance(0.15) && Schemes.TryGetValue(IncentiveSchemeDefaults.InvoiceDiscount50, out var discount))
        {
            quote.Incentives.Add(new QuoteIncentive
            {
                IncentiveSchemeId = discount.Id,
                Amount = QuotePricingService.InvoiceDiscountTotal(discount, finalRounded + discountBasisExtra),
                Confirmed = status == QuoteStatusCodes.Confirmed && Chance(0.6),
            });
        }

        if (Chance(0.15))
        {
            var (city, cap) = Pick(Cities);
            quote.InstallationAddress = $"{Pick(Streets)} {Rng.Next(1, 120)}";
            quote.InstallationCity = $"{cap} {city}";
        }

        // Status history + denormalised current status.
        var changedBy = Pick(operators).Email;
        quote.StatusEvents.Add(new QuoteStatusEvent { Status = QuoteStatusCodes.Draft, OccurredAt = createdAt, ChangedBy = changedBy });
        var t = createdAt;
        if (status is QuoteStatusCodes.SurveyDone or QuoteStatusCodes.Confirmed
            || (status == QuoteStatusCodes.Recalled && Chance(0.6)))
        {
            t = t.AddDays(Rng.Next(2, 16));
            quote.StatusEvents.Add(new QuoteStatusEvent
            {
                Status = QuoteStatusCodes.SurveyDone, OccurredAt = t, ChangedBy = changedBy,
                Notes = Pick(StatusNotes),
            });
        }
        if (status is QuoteStatusCodes.Recalled or QuoteStatusCodes.Confirmed)
        {
            t = t.AddDays(Rng.Next(3, 30));
            quote.StatusEvents.Add(new QuoteStatusEvent
            {
                Status = status, OccurredAt = t, ChangedBy = changedBy, Notes = Pick(StatusNotes),
            });
        }

        // Attachments / generated doc URLs (placeholder storage paths).
        if (Chance(0.25))
        {
            var n = Rng.Next(1, 3);
            for (var a = 0; a < n; a++)
            {
                quote.Attachments.Add(new QuoteAttachment
                {
                    FileName = $"foto-sopralluogo-{a + 1}.jpg",
                    FileUrl = $"{DemoAssetBaseUrl}/quote-{number}/foto-{a + 1}.jpg",
                    UploadedAt = createdAt.AddDays(Rng.Next(0, 10)),
                });
            }
        }
        if (Chance(0.35))
        {
            quote.PdfUrl = $"{DemoAssetBaseUrl}/quote-{number}/preventivo.pdf";
        }
        if (Chance(0.2))
        {
            quote.FloorPlanImageUrl = $"{DemoAssetBaseUrl}/quote-{number}/planimetria.png";
        }

        return quote;
    }

    private static List<Quote> BuildVersionedRevisions(List<Quote> quotes, List<Operator> operators)
    {
        var result = new List<Quote>();
        var confirmed = quotes.Where(q => q.CurrentStatus == QuoteStatusCodes.Confirmed).ToList();
        var take = (int)(confirmed.Count * VersionedQuoteFraction);

        foreach (var parent in TakeRandom(confirmed, take))
        {
            var createdAt = parent.CreatedAt.AddDays(Rng.Next(10, 60));
            var child = new Quote
            {
                Number = parent.Number,
                Version = 2,
                Revision = 0,
                ParentQuoteId = parent.Id,
                ClientId = parent.ClientId,
                OperatorId = parent.OperatorId,
                Department = parent.Department,
                CreatedAt = createdAt,
                CurrentStatus = Chance(0.5) ? QuoteStatusCodes.Confirmed : QuoteStatusCodes.Draft,
                Predisposed = parent.Predisposed,
                Vat = parent.Vat,
                ManualDiscount = parent.ManualDiscount,
                MachinesPrice = parent.MachinesPrice,
                InstallationPrice = Round2(parent.InstallationPrice * 1.05m),
                InstallationPositionNotes = parent.InstallationPositionNotes,
            };
            var raw = child.MachinesPrice + child.InstallationPrice - child.ManualDiscount;
            child.FinalPrice = Math.Ceiling(raw / 10m) * 10m;
            child.Rounding = Round2(child.FinalPrice - raw);

            foreach (var p in parent.Products)
            {
                child.Products.Add(new QuoteProduct
                {
                    ProductId = p.ProductId, Role = p.Role, Quantity = p.Quantity,
                    Price = p.Price, DiscountedPrice = p.DiscountedPrice,
                    Incentives = p.Incentives
                        .Select(i => new QuoteProductIncentive { IncentiveSchemeId = i.IncentiveSchemeId, Amount = i.Amount })
                        .ToList(),
                });
            }
            foreach (var s in parent.ServiceItems)
            {
                child.ServiceItems.Add(new QuoteServiceItem { ServiceItemId = s.ServiceItemId, Quantity = s.Quantity, UnitPrice = s.UnitPrice });
            }

            var changedBy = Pick(operators).Email;
            child.StatusEvents.Add(new QuoteStatusEvent { Status = QuoteStatusCodes.Draft, OccurredAt = createdAt, ChangedBy = changedBy, Notes = "Nuova versione dopo revisione prezzi." });
            if (child.CurrentStatus == QuoteStatusCodes.Confirmed)
            {
                child.StatusEvents.Add(new QuoteStatusEvent { Status = QuoteStatusCodes.Confirmed, OccurredAt = createdAt.AddDays(Rng.Next(2, 20)), ChangedBy = changedBy });
            }
            result.Add(child);
        }
        return result;
    }

    private static QuoteProduct NewQuoteProduct(Product product, ProductRole role, int qty)
    {
        var discount = role == ProductRole.ExternalUnit ? 0.05m : Pick(new[] { 0.05m, 0.10m, 0.12m, 0.15m });
        var line = new QuoteProduct
        {
            ProductId = product.Id,
            Role = role,
            Quantity = qty,
            Price = product.Price,
            DiscountedPrice = Round2(product.Price * (1m - discount)),
        };
        if (role == ProductRole.InternalUnit && Chance(0.2)
            && Schemes.TryGetValue(IncentiveSchemeDefaults.AllInclusiveWarranty, out var warranty))
        {
            line.Incentives.Add(new QuoteProductIncentive
            {
                IncentiveSchemeId = warranty.Id,
                Amount = QuotePricingService.LineAmount(warranty, product.Price, qty),
            });
        }
        return line;
    }

    private static decimal DiscountedLine(Product product, int qty) =>
        Round2(product.Price * 0.9m) * qty;

    // ---------------------------------------------------------------- repairs

    private static readonly string[] RepairBrands =
    {
        "Daikin", "Mitsubishi Electric", "Toshiba", "Samsung", "Hisense", "Panasonic", "LG", "Argo", "Olimpia Splendid",
    };

    private static readonly string[] RepairFaults =
    {
        "Unità interna non raffredda, perdita di gas sospetta sul giunto.",
        "Errore U4 lampeggiante, comunicazione interna/esterna assente.",
        "Rumorosità anomala ventola unità esterna, cuscinetto da sostituire.",
        "Perdita d'acqua dalla bacinella di condensa, scarico ostruito.",
        "Scheda di potenza unità esterna bruciata dopo sbalzo di tensione.",
        "Telecomando non risponde, sostituzione ricevitore IR.",
        "Compressore non parte, condensatore di spunto esausto.",
        "Sensore temperatura ambiente fuori taratura, letture errate.",
        "Sbrinamento non funzionante, formazione di ghiaccio sulla batteria.",
    };

    private static readonly string[] RevisionTasks =
    {
        "Revisione stagionale: pulizia filtri e batteria, controllo carica gas, serraggio morsetti.",
        "Manutenzione ordinaria impianto multisplit, sanificazione unità interne.",
        "Controllo pressioni e tenuta circuito frigorifero, verifica scarichi condensa.",
        "Pulizia unità esterna, controllo assorbimenti elettrici e delta T.",
        "Sanificazione con prodotto certificato, sostituzione filtri a carboni attivi.",
    };

    private static readonly string?[] ScheduleNotes =
    {
        "Concordare orario con il cliente il giorno prima.", "Portare scala doppia, esterna al primo piano.",
        "Cliente disponibile solo di mattina.", "Serve autorizzazione portineria per accesso cortile.",
        null, null,
    };

    private static Repair BuildRepair(
        Client client, Quote? originQuote, List<ServiceItem> serviceItems,
        List<Installer> installers, List<Operator> operators)
    {
        var type = Chance(0.32) ? RepairType.Revision : RepairType.Repair;
        var status = Weighted(
            (RepairStatusCodes.New, 14), (RepairStatusCodes.Urgent, 8), (RepairStatusCodes.Scheduled, 16),
            (RepairStatusCodes.InProgress, 14), (RepairStatusCodes.AwaitingPayment, 8),
            (RepairStatusCodes.PendingOffSeason, 6), (RepairStatusCodes.ClosedByInstallers, 8),
            (RepairStatusCodes.Closed, 16), (RepairStatusCodes.AssistanceRequested, 5),
            (RepairStatusCodes.AssistanceContacted, 5));

        var insertedAt = RecentUtc(5, 430);
        var hasDate = status is RepairStatusCodes.Scheduled or RepairStatusCodes.InProgress
            or RepairStatusCodes.AwaitingPayment or RepairStatusCodes.ClosedByInstallers or RepairStatusCodes.Closed;

        var repair = new Repair
        {
            Type = type,
            Status = status,
            ClientId = client.Id,
            QuoteId = originQuote?.Id,
            Brand = Pick(RepairBrands),
            Description = type == RepairType.Revision ? Pick(RevisionTasks) : Pick(RepairFaults),
            Urgent = status == RepairStatusCodes.Urgent || Chance(0.12),
            Predisposed = Chance(0.3),
            AllInclusive = type == RepairType.Revision || Chance(0.2),
            UnitCount = Rng.Next(1, 4),
            SplitCount = Rng.Next(1, 5),
            Rounding = Chance(0.4) ? Round2(Rng.Next(0, 5) * 2m) : 0m,
            InsertedAt = insertedAt,
            InstallationDate = hasDate ? insertedAt.AddDays(Rng.Next(3, 32)) : null,
            PdfUrl = Chance(0.2)
                ? $"{DemoAssetBaseUrl}/repair/rapporto.pdf"
                : null,
        };

        foreach (var si in TakeRandom(serviceItems, Rng.Next(1, 4)))
        {
            repair.ServiceItems.Add(new RepairServiceItem
            {
                ServiceItemId = si.Id,
                Quantity = Rng.Next(1, 4),
                UnitPrice = si.Price,
            });
        }

        if (hasDate && repair.InstallationDate is { } d)
        {
            var entries = Rng.Next(1, 3);
            for (var e = 0; e < entries; e++)
            {
                repair.ScheduleEntries.Add(new RepairScheduleEntry
                {
                    InstallerId = Chance(0.85) ? Pick(installers).Id : null,
                    Date = d.AddDays(e * Rng.Next(1, 4)),
                    Notes = Pick(ScheduleNotes),
                });
            }
        }

        var changedBy = Pick(operators).Email;
        repair.StatusEvents.Add(new RepairStatusEvent { Status = RepairStatusCodes.New, OccurredAt = insertedAt, ChangedBy = changedBy });
        if (status != RepairStatusCodes.New)
        {
            if (status is RepairStatusCodes.Closed or RepairStatusCodes.ClosedByInstallers or RepairStatusCodes.AwaitingPayment)
            {
                repair.StatusEvents.Add(new RepairStatusEvent
                {
                    Status = RepairStatusCodes.InProgress, OccurredAt = insertedAt.AddDays(Rng.Next(1, 12)), ChangedBy = changedBy,
                });
            }
            repair.StatusEvents.Add(new RepairStatusEvent
            {
                Status = status, OccurredAt = insertedAt.AddDays(Rng.Next(13, 40)), ChangedBy = changedBy,
                Notes = Chance(0.3) ? "Aggiornamento stato da app installatori." : null,
            });
        }

        return repair;
    }

    // ---------------------------------------------------------------- installations

    private static readonly string?[] InstallationNotes =
    {
        "Squadra completa, giornata intera prevista.", "Portare recuperatore gas e bilancia.",
        "Accesso con furgone dal cortile interno.", "Cliente fornisce parcheggio riservato.",
        "Coordinarsi con l'elettricista per la linea dedicata.", null, null,
    };

    private static List<ScheduledInstallation> BuildInstallations(List<Quote> confirmed, List<Installer> installers)
    {
        var crews = CrewsOf(installers);
        var list = new List<ScheduledInstallation>();
        foreach (var quote in confirmed)
        {
            if (!Chance(0.85))
            {
                continue;
            }

            var inPast = Chance(0.6);
            var date = inPast ? RecentUtc(2, 170) : DateTime.UtcNow.Date.AddDays(Rng.Next(1, 56));
            date = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

            var slot = Weighted((DaySlotCodes.Morning, 40), (DaySlotCodes.Afternoon, 35), (DaySlotCodes.FullDay, 25));

            var install = new ScheduledInstallation
            {
                QuoteId = quote.Id,
                Date = date,
                Slot = slot,
                Notes = Pick(InstallationNotes),
                ReportPdfUrl = inPast && Chance(0.5)
                    ? $"{DemoAssetBaseUrl}/installation/rapporto-intervento.pdf"
                    : null,
                UpdatedAt = Chance(0.3) ? date.AddDays(-Rng.Next(1, 7)) : null,
                UpdatedBy = Chance(0.3) ? "planning@example.com" : null,
            };

            AssignCrew(install, crews, installers);
            list.Add(install);
        }
        return list;
    }

    /// <summary>
    /// A dense installation band from <see cref="CalendarFillRadiusDays"/> before "today" to the
    /// same span after, so both the week and the month calendar views are genuinely full: several
    /// crews busy on most working days, a healthy share of full-day bookings to exercise the
    /// mattina/pomeriggio → giornata collapse, and the odd split day where one crew works both
    /// halves. Every row links to a random confirmed quote (a quote may back several).
    /// </summary>
    private static List<ScheduledInstallation> BuildCalendarFill(List<Quote> confirmed, List<Installer> installers)
    {
        var list = new List<ScheduledInstallation>();
        var crews = CrewsOf(installers);
        if (confirmed.Count == 0 || crews.Count == 0)
        {
            return list;
        }

        var today = DateTime.UtcNow.Date;
        for (var offset = -CalendarFillRadiusDays; offset <= CalendarFillRadiusDays; offset++)
        {
            var date = DateTime.SpecifyKind(today.AddDays(offset), DateTimeKind.Utc);
            if (date.DayOfWeek == DayOfWeek.Sunday)
            {
                continue;
            }

            var isSaturday = date.DayOfWeek == DayOfWeek.Saturday;
            var busyCrews = TakeRandom(crews, isSaturday ? Rng.Next(0, 3) : Rng.Next(3, crews.Count + 1));
            foreach (var crew in busyCrews)
            {
                var pattern = Rng.NextDouble();
                if (pattern < 0.45)
                {
                    list.Add(NewFill(Pick(confirmed), date, DaySlotCodes.FullDay, crew));
                }
                else if (pattern < 0.75)
                {
                    list.Add(NewFill(Pick(confirmed), date, DaySlotCodes.Morning, crew));
                }
                else if (pattern < 0.90)
                {
                    list.Add(NewFill(Pick(confirmed), date, DaySlotCodes.Morning, crew));
                    list.Add(NewFill(Pick(confirmed), date, DaySlotCodes.Afternoon, crew));
                }
                else
                {
                    list.Add(NewFill(Pick(confirmed), date, DaySlotCodes.Afternoon, crew));
                }
            }
        }
        return list;
    }

    private static ScheduledInstallation NewFill(Quote quote, DateTime date, string slot, List<Installer> crew)
    {
        var inPast = date < DateTime.UtcNow.Date;
        var install = new ScheduledInstallation
        {
            QuoteId = quote.Id,
            Date = date,
            Slot = slot,
            Notes = Pick(InstallationNotes),
            ReportPdfUrl = inPast && Chance(0.5)
                ? $"{DemoAssetBaseUrl}/installation/rapporto-intervento.pdf"
                : null,
            UpdatedAt = Chance(0.25) ? date.AddDays(-Rng.Next(1, 7)) : null,
            UpdatedBy = Chance(0.25) ? "planning@example.com" : null,
        };
        foreach (var installer in crew)
        {
            install.Installers.Add(new InstallationInstaller { InstallerId = installer.Id });
        }
        return install;
    }

    private static List<List<Installer>> CrewsOf(List<Installer> installers) =>
        installers.Where(i => i.Crew is not null)
            .GroupBy(i => i.Crew!.Value)
            .OrderBy(g => g.Key)
            .Select(g => g.ToList())
            .ToList();

    /// <summary>~80% a whole crew, ~12% a lone installer (lands in the "no crew" lane), ~8% nobody yet.</summary>
    private static void AssignCrew(ScheduledInstallation install, List<List<Installer>> crews, List<Installer> allInstallers)
    {
        var roll = Rng.NextDouble();
        if (roll < 0.80 && crews.Count > 0)
        {
            foreach (var installer in Pick(crews))
            {
                install.Installers.Add(new InstallationInstaller { InstallerId = installer.Id });
            }
        }
        else if (roll < 0.92)
        {
            install.Installers.Add(new InstallationInstaller { InstallerId = Pick(allInstallers).Id });
        }
    }

    // ---------------------------------------------------------------- F-Gas

    private static List<FGasRecord> BuildFGasRecords(List<Quote> quotes, List<Repair> repairs)
    {
        var list = new List<FGasRecord>(FGasCount);
        for (var i = 0; i < FGasCount; i++)
        {
            var gas = Weighted(("R32", 75), ("R410A", 18), ("R290", 7));
            var kg = Round2((decimal)(Rng.NextDouble() * 3.25 + 0.35));
            var record = new FGasRecord
            {
                GasType = gas,
                KilogramsOfGas = kg,
                IsRefill = Chance(0.35),
                HasExtraCharge = Chance(0.25),
            };

            // Exactly one parent (DB CHECK: (quote_id IS NULL) <> (repair_id IS NULL)).
            if (Chance(0.55))
            {
                var q = quotes[Rng.Next(quotes.Count)];
                record.QuoteId = q.Id;
                record.RecordedAt = q.CreatedAt.AddDays(Rng.Next(1, 45));
            }
            else
            {
                var r = repairs[Rng.Next(repairs.Count)];
                record.RepairId = r.Id;
                record.RecordedAt = r.InsertedAt.AddDays(Rng.Next(1, 30));
            }

            list.Add(record);
        }
        return list;
    }

    // ---------------------------------------------------------------- helpers

    private static T Pick<T>(IReadOnlyList<T> list) => list[Rng.Next(list.Count)];

    private static bool Chance(double p) => Rng.NextDouble() < p;

    private static IEnumerable<T> TakeRandom<T>(IReadOnlyList<T> source, int count)
    {
        if (count <= 0 || source.Count == 0)
        {
            yield break;
        }
        count = Math.Min(count, source.Count);
        var indices = Enumerable.Range(0, source.Count).ToList();
        for (var i = 0; i < count; i++)
        {
            var j = Rng.Next(indices.Count);
            yield return source[indices[j]];
            indices.RemoveAt(j);
        }
    }

    private static T Weighted<T>(params (T Value, int Weight)[] options)
    {
        var total = options.Sum(o => o.Weight);
        var roll = Rng.Next(total);
        var acc = 0;
        foreach (var (value, weight) in options)
        {
            acc += weight;
            if (roll < acc)
            {
                return value;
            }
        }
        return options[^1].Value;
    }

    private static DateTime RecentUtc(int minDaysAgo, int maxDaysAgo) =>
        DateTime.SpecifyKind(
            DateTime.UtcNow.AddDays(-Rng.Next(minDaysAgo, maxDaysAgo)).AddHours(-Rng.Next(0, 24)),
            DateTimeKind.Utc);

    private static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string RandomDigits(int n)
    {
        Span<char> buf = stackalloc char[n];
        for (var i = 0; i < n; i++)
        {
            buf[i] = (char)('0' + Rng.Next(10));
        }
        return new string(buf);
    }

    private static readonly char[] CfConsonants = "BCDFGHJKLMNPQRSTVWXYZ".ToCharArray();

    /// <summary>A codice-fiscale-shaped 16-char string — plausible format, not a valid checksum.</summary>
    private static string FakeCodiceFiscale(string first, string last)
    {
        string Triple(string s)
        {
            var up = new string(s.ToUpperInvariant().Where(char.IsLetter).ToArray());
            var cons = new string(up.Where(c => Array.IndexOf(CfConsonants, c) >= 0).ToArray());
            var padded = (cons + up + "XXX");
            return padded[..3];
        }

        var year = Rng.Next(45, 99).ToString("D2");
        var month = "ABCDEHLMPRST"[Rng.Next(12)];
        var day = Rng.Next(1, 28).ToString("D2");
        var area = $"{CfConsonants[Rng.Next(CfConsonants.Length)]}{Rng.Next(100, 999)}";
        var check = CfConsonants[Rng.Next(CfConsonants.Length)];
        return $"{Triple(last)}{Triple(first)}{year}{month}{day}{area}{check}";
    }
}
