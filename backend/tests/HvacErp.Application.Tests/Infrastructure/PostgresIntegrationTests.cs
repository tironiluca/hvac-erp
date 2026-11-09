using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using HvacErp.Application.Pdf;
using HvacErp.Application.Pricing;
using HvacErp.Application.Services;
using HvacErp.Domain.Entities;
using HvacErp.Infrastructure.Persistence;
using HvacErp.Infrastructure.Persistence.Seed;

namespace HvacErp.Application.Tests.Infrastructure;

/// <summary>
/// Real-Postgres checks the in-memory provider cannot give: the whole migration chain applies to an
/// empty database, and LINQ in the services translates to SQL. Runs only when
/// <c>POSTGRES_TEST_CONNECTION</c> points at a server whose user may create databases (CI provides
/// one); a scratch database is created and dropped per run.
/// </summary>
public class PostgresIntegrationTests
{
    private static readonly string? Server = Environment.GetEnvironmentVariable("POSTGRES_TEST_CONNECTION");

    [Fact]
    public async Task Migrations_apply_and_dashboard_queries_translate_on_postgres()
    {
        if (Server is null) return;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql($"{Server};Database=hvac_erp_it_{Guid.NewGuid():N}")
            .UseSnakeCaseNamingConvention()
            .Options;

        await using var db = new AppDbContext(options);
        try
        {
            await db.Database.MigrateAsync();
            await DevDataSeeder.SeedAsync(db);
            await DemoDataSeeder.SeedAsync(db);

            var dashboard = await new DashboardService(db, TimeProvider.System).GetAsync(null);

            Assert.NotEmpty(dashboard.OpenRepairsByStatus);
            Assert.True(dashboard.Quotes.AwaitingSurvey + dashboard.Quotes.AwaitingRecall + dashboard.Quotes.AwaitingConfirmation > 0);
            Assert.NotEmpty(dashboard.RecentActivity);
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    /// <summary>
    /// EnglishDefaultsAndStableCodes against data shaped like an existing Italian deployment: the
    /// pricing rows get their codes (one per legacy name, even with a duplicate), categories become
    /// Outdoor/Indoor, the "Idronico" brand sets the flag, and stored templates become Italian.
    /// </summary>
    [Fact]
    public async Task English_defaults_migration_backfills_an_existing_italian_database()
    {
        if (Server is null) return;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql($"{Server};Database=hvac_erp_it_{Guid.NewGuid():N}")
            .UseSnakeCaseNamingConvention()
            .Options;

        await using var db = new AppDbContext(options);
        try
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261106103510_EnableRowLevelSecurity");

            // Raw SQL is a format string here: JSON braces are doubled.
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO service_items (name, price) VALUES
                    ('Mono', 350), ('Dual', 550), ('Trial', 750), ('Poker', 950), ('Penta', 1150),
                    ('Plenum canalizzato', 120), ('Modulo Cloud canalizzato', 180),
                    ('Tubo flessibile canalizzato (al metro)', 22), ('Tubo rigido canalizzato (al metro)', 28),
                    ('Telecomando canalizzato', 45), ('Mono', 999), ('Staffa a muro', 35);

                INSERT INTO product_price_lists (description, vat, markup, initial_discount, transport_fee,
                    waste_disposal_fee, deposit_fee, covers_external_code)
                VALUES ('Listino', 22, 30, 0, 0, 0, 0, false);

                INSERT INTO products (price_list_id, brand, model, category, extra_price, stock, price, wifi, active)
                SELECT id, b.brand, b.model, b.category, 0, 0, 100, false, true
                FROM product_price_lists,
                     (VALUES ('Daikin', 'A', 'Esterna'), ('Daikin', 'B', 'Interna'), ('Idronico', 'C', 'Esterna')) AS b(brand, model, category);

                DELETE FROM pdf_templates;
                INSERT INTO pdf_templates (document_type, name, is_active, is_system_default, definition_json, updated_at)
                VALUES ('Quote', 'Preventivo', true, true,
                    '{{"page":{{"size":"A4","margin":30,"fontFamily":"Calibri","baseFontSize":10,"accentColor":"#BBDEFB"}}}}', now());
                """);

            await db.Database.MigrateAsync();

            var codes = await db.ServiceItems.Where(s => s.Code != null)
                .ToDictionaryAsync(s => s.Code!, s => s.Price);
            Assert.Equal(10, codes.Count);
            Assert.Equal(350m, codes[InstallationCostItems.BaseSystemPriceFor(1)]); // the lowest id wins
            Assert.Equal(45m, codes[InstallationCostItems.Remote]);
            Assert.Null((await db.ServiceItems.SingleAsync(s => s.Name == "Staffa a muro")).Code);

            var products = await db.Products.OrderBy(p => p.Model).ToListAsync();
            Assert.Equal([ProductCategories.Outdoor, ProductCategories.Indoor, ProductCategories.Outdoor],
                products.Select(p => p.Category));
            Assert.Equal([false, false, true], products.Select(p => p.IsHydronic));

            var template = await db.PdfTemplates.SingleAsync();
            Assert.Equal(PdfLabels.Italian, PdfJson.Deserialize(template.DefinitionJson).Page.Language);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    /// <summary>
    /// IncentiveSchemes (and the later QuoteLevelWarranty) against an existing Italian deployment: the ENEA / RAEE / invoice-discount
    /// flags and the line warranties become scheme rows with the same amounts and acceptance, the
    /// schemes are seeded in Italian, the quote template's options are mapped, and Down restores the
    /// old columns.
    /// </summary>
    [Fact]
    public async Task Incentive_schemes_migration_moves_existing_quote_data_and_rolls_back()
    {
        if (Server is null) return;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql($"{Server};Database=hvac_erp_it_{Guid.NewGuid():N}")
            .UseSnakeCaseNamingConvention()
            .Options;

        await using var db = new AppDbContext(options);
        try
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261107192453_EnglishDefaultsAndStableCodes");

            // Raw SQL is a format string here: JSON braces are doubled.
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO clients (code, full_name) VALUES ('C1', 'Cliente');
                INSERT INTO product_price_lists (description, vat, markup, initial_discount, transport_fee,
                    waste_disposal_fee, deposit_fee, covers_external_code)
                VALUES ('Listino', 22, 30, 0, 0, 0, 0, false);
                INSERT INTO products (price_list_id, brand, model, category, extra_price, stock, price, wifi, active, is_hydronic)
                SELECT id, 'Daikin', 'A', 'Indoor', 0, 0, 500, false, true, false FROM product_price_lists;

                INSERT INTO quotes (number, version, revision, client_id, department, created_at, predisposed, vat,
                    manual_discount, machines_price, installation_price, rounding, final_price,
                    enea_eligible, enea_price, raee_eligible, raee_price, sf_requested, sf_confirmed, sf_price)
                SELECT n, 1, 0, c.id, 'Store', now(), true, 'Standard22', 0, 0, 0, 0, 1000,
                    n = 1, CASE WHEN n = 1 THEN 150 END, n = 1, CASE WHEN n = 1 THEN 30 END,
                    n <= 2, n = 1, CASE WHEN n <= 2 THEN 1350 END
                FROM clients c, generate_series(1, 3) AS n;

                INSERT INTO quote_products (quote_id, product_id, role, quantity, price, discounted_price, warranty, warranty_price)
                SELECT q.id, p.id, 'InternalUnit', 1, 500, 450, q.number = 1, CASE WHEN q.number = 1 THEN 40 END
                FROM quotes q, products p;

                DELETE FROM pdf_templates;
                INSERT INTO pdf_templates (document_type, name, is_active, is_system_default, definition_json, updated_at)
                VALUES ('Quote', 'Preventivo', true, true,
                    '{{"page":{{"language":"it"}},"sections":[{{"type":"quote.documentTitle","options":{{}}}},{{"type":"quote.pricingSummary","options":{{"showEnea":false,"showRaee":true,"showSfBlock":false,"showWarrantyNote":true}}}}]}}', now());
                """);

            await db.Database.MigrateAsync();

            var schemes = await db.IncentiveSchemes.ToDictionaryAsync(s => s.Code);
            Assert.Equal("Sconto in fattura 50%", schemes[IncentiveSchemeDefaults.InvoiceDiscount50].Label);

            var incentives = await db.QuoteIncentives.Include(i => i.Quote).Include(i => i.IncentiveScheme).ToListAsync();
            var first = incentives.Where(i => i.Quote.Number == 1).ToDictionary(i => i.IncentiveScheme.Code);
            Assert.Equal(150m, first[IncentiveSchemeDefaults.EnergyTaxDeduction].Amount);
            Assert.Equal(30m, first[IncentiveSchemeDefaults.WeeeTakeBack].Amount);
            Assert.Equal((1350m, true), (first[IncentiveSchemeDefaults.InvoiceDiscount50].Amount, first[IncentiveSchemeDefaults.InvoiceDiscount50].Confirmed));
            var second = Assert.Single(incentives, i => i.Quote.Number == 2);
            Assert.Equal((IncentiveSchemeDefaults.InvoiceDiscount50, false), (second.IncentiveScheme.Code, second.Confirmed));
            Assert.DoesNotContain(incentives, i => i.Quote.Number == 3);

            // QuoteLevelWarranty: the line warranty became one fixed amount on its quote, total unchanged.
            var warranty = first[IncentiveSchemeDefaults.AllInclusiveWarranty];
            Assert.Equal((AmountModes.Fixed, 40m, 40m), (warranty.Mode, warranty.Value, warranty.Amount));
            Assert.Equal(IncentiveKinds.QuoteAmount, schemes[IncentiveSchemeDefaults.AllInclusiveWarranty].Kind);
            Assert.True(schemes[IncentiveSchemeDefaults.AllInclusiveWarranty].ChargeBeforeVat);

            var summary = PdfJson.Deserialize((await db.PdfTemplates.SingleAsync()).DefinitionJson)
                .Sections.Single(s => s.Type == "quote.pricingSummary");
            Assert.Equal(new Dictionary<string, bool> { ["showIncentives"] = true, ["showInvoiceDiscount"] = false }, summary.Options);

            // Down: the old columns come back with the same values.
            await migrator.MigrateAsync("20261107192453_EnglishDefaultsAndStableCodes");
            var restored = await db.Database
                .SqlQueryRaw<string>("""
                    SELECT string_agg(concat_ws(':', number, enea_eligible, enea_price, raee_eligible, raee_price,
                        sf_requested, sf_confirmed, sf_price), ' ' ORDER BY number) AS "Value" FROM quotes
                    """)
                .SingleAsync();
            Assert.Equal("1:t:150.00:t:30.00:t:t:1350.00 2:f:f:t:f:1350.00 3:f:f:f:f", restored);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}
