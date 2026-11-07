using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using HvacErp.Application.Pdf;
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
}
