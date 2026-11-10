using Microsoft.EntityFrameworkCore;
using Npgsql;
using HvacErp.Infrastructure.Persistence;
using HvacErp.Infrastructure.Persistence.Migrations;

namespace HvacErp.Application.Tests.Infrastructure;

public class RowLevelSecurityCoverageTests
{
    /// <summary>
    /// Every table in the EF model must have RLS enabled. A new entity's migration must ship its own
    /// ENABLE ROW LEVEL SECURITY + policies (see the README's Authorization section) and then be
    /// listed here, so this check fails loudly instead of leaving a table open to the Data API.
    /// </summary>
    private static readonly string[] SecuredByLaterMigrations = [.. IncentiveSchemes.SecuredTables, .. ErpIntegration.SecuredTables, .. MultiTenancy.SecuredTables];

    [Fact]
    public void Every_model_table_is_covered_by_a_row_level_security_migration()
    {
        // The Npgsql provider (not InMemory) is needed for the snake_case relational table names;
        // building the model never opens a connection.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(new NpgsqlDataSourceBuilder("Host=unused").Build())
            .UseSnakeCaseNamingConvention()
            .Options;
        using var db = new AppDbContext(options);

        var modelTables = db.Model.GetEntityTypes()
            .Select(e => e.GetTableName())
            .OfType<string>()
            .Append("__EFMigrationsHistory")
            .ToHashSet();

        var secured = EnableRowLevelSecurity.AllTables.Concat(SecuredByLaterMigrations)
            .Except(QuoteLevelWarranty.DroppedTables)
            .ToHashSet();

        Assert.Empty(modelTables.Except(secured));
        Assert.Empty(secured.Except(modelTables)); // catches typos in the migration's table lists
    }
}
