using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using HvacErp.Application.Common;
using HvacErp.Application.Tests.Support;
using HvacErp.Infrastructure.Persistence;

namespace HvacErp.Application.Tests.Infrastructure;

// The only values interpolated into SQL in this file are GUIDs the tests generate themselves.
#pragma warning disable EF1002

/// <summary>
/// What only a real Postgres can prove about multi-tenancy: the migration moves an existing
/// single-company database and rolls back, the composite foreign keys reject cross-tenant
/// references, and the row-level-security policies isolate tenants and stores. Runs only when
/// <c>POSTGRES_TEST_CONNECTION</c> points at a server whose user may create databases and roles.
/// </summary>
public class MultiTenancyPostgresTests
{
    private const string BeforeMultiTenancy = "20261109174501_ErpIntegration";
    private static readonly string? Server = Environment.GetEnvironmentVariable("POSTGRES_TEST_CONNECTION");

    private static readonly Guid Admin = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid Sales = Guid.Parse("00000000-0000-0000-0000-0000000000a2");

    private static AppDbContext Open(string database, ITenantContext? tenant = null) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql($"{Server};Database={database}")
            .UseSnakeCaseNamingConvention()
            .Options, tenant ?? TestDb.DefaultTenant);

    [Fact]
    public async Task Migration_moves_a_single_company_database_to_the_default_tenant_and_rolls_back()
    {
        if (Server is null) return;

        await using var db = Open($"hvac_erp_mt_{Guid.NewGuid():N}");
        try
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(BeforeMultiTenancy);

            await db.Database.ExecuteSqlRawAsync($"""
                UPDATE portal_brandings SET portal_title = 'Demo Company';
                INSERT INTO installers (name) VALUES ('Luca');
                INSERT INTO users (id, email, display_name, role, installer_id, created_at)
                SELECT '{Admin}', 'admin@example.com', 'Admin', 'Admin', id, now() FROM installers;
                INSERT INTO users (id, email, display_name, role, created_at)
                VALUES ('{Sales}', 'sales@example.com', 'Sales', 'Sales', now());
                INSERT INTO clients (code, full_name) VALUES ('C1', 'Existing client');
                INSERT INTO quotes (number, version, revision, client_id, department, created_at, predisposed, vat,
                    manual_discount, machines_price, installation_price, rounding, final_price, current_status)
                SELECT 7, 1, 0, id, 'Store', now(), false, 'Standard22', 0, 0, 0, 0, 0, 'Draft' FROM clients;
                INSERT INTO quote_status_events (quote_id, status, occurred_at) SELECT id, 'Draft', now() FROM quotes;
                """);

            await db.Database.MigrateAsync();

            // Existing rows belong to tenant 1 / store 1, named after the deployment's portal title.
            Assert.Equal("Demo Company", await Scalar<string>(db, "SELECT name FROM tenants WHERE id = 1"));
            Assert.Equal(1L, await Scalar<long>(db, "SELECT count(*) FROM stores WHERE id = 1 AND tenant_id = 1"));
            Assert.Equal("1/1", await Scalar<string>(db, "SELECT tenant_id || '/' || store_id FROM quotes"));
            Assert.Equal(0L, await Scalar<long>(db, """
                SELECT (SELECT count(*) FROM clients WHERE tenant_id <> 1) + (SELECT count(*) FROM quote_status_events WHERE tenant_id <> 1)
                     + (SELECT count(*) FROM pdf_templates WHERE tenant_id <> 1) + (SELECT count(*) FROM incentive_schemes WHERE tenant_id <> 1)
                """));

            // Roles moved into memberships of the default tenant; the Admin runs the platform.
            Assert.Equal("Admin:t:t:t,Sales:t:f:f", await Scalar<string>(db, """
                SELECT string_agg(concat_ws(':', m.role, m.all_stores, m.installer_id IS NOT NULL, u.is_platform_admin), ',' ORDER BY m.role)
                FROM tenant_memberships m JOIN users u ON u.id = m.user_id
                """));
            Assert.Equal(0L, await Scalar<long>(db,
                "SELECT count(*) FROM information_schema.columns WHERE table_name = 'users' AND column_name IN ('role', 'installer_id')"));

            // A second tenant: codes and quote numbers are free again, references across are not.
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO tenants (name, is_active, created_at) VALUES ('Second', true, now());
                INSERT INTO stores (tenant_id, name, is_active, created_at) SELECT id, 'Second main', true, now() FROM tenants WHERE name = 'Second';
                INSERT INTO clients (tenant_id, code, full_name) SELECT id, 'C1', 'Same code, other company' FROM tenants WHERE name = 'Second';
                INSERT INTO quotes (tenant_id, store_id, number, version, revision, client_id, department, created_at, predisposed, vat,
                    manual_discount, machines_price, installation_price, rounding, final_price, current_status)
                SELECT c.tenant_id, s.id, 7, 1, 0, c.id, 'Store', now(), false, 'Standard22', 0, 0, 0, 0, 0, 'Draft'
                FROM clients c JOIN stores s ON s.tenant_id = c.tenant_id WHERE c.tenant_id <> 1;
                """);

            await AssertRejected(db, PostgresErrorCodes.ForeignKeyViolation, "fk_quotes_client_id_tenant", """
                INSERT INTO quotes (tenant_id, store_id, number, version, revision, client_id, department, created_at, predisposed, vat,
                    manual_discount, machines_price, installation_price, rounding, final_price, current_status)
                SELECT 1, 1, 8, 1, 0, id, 'Store', now(), false, 'Standard22', 0, 0, 0, 0, 0, 'Draft' FROM clients WHERE tenant_id <> 1
                """);
            await AssertRejected(db, PostgresErrorCodes.ForeignKeyViolation, "fk_quotes_store",
                "UPDATE quotes SET store_id = (SELECT id FROM stores WHERE tenant_id <> 1), number = 99 WHERE tenant_id = 1");
            await AssertRejected(db, PostgresErrorCodes.UniqueViolation, "ix_clients_tenant_id_code",
                "INSERT INTO clients (tenant_id, code, full_name) VALUES (1, 'C1', 'Duplicate in the same company')");
            // No default owner: a row that does not say whose it is cannot be inserted.
            await AssertRejected(db, PostgresErrorCodes.NotNullViolation, "tenant_id",
                "INSERT INTO clients (code, full_name) VALUES ('C2', 'Ownerless')");

            // The composite keys follow the ordinary ones: SET NULL clears only the id, CASCADE still cascades.
            await db.Database.ExecuteSqlRawAsync("DELETE FROM installers");
            Assert.Equal("1:t", await Scalar<string>(db,
                $"SELECT concat_ws(':', tenant_id, installer_id IS NULL) FROM tenant_memberships WHERE user_id = '{Admin}'"));
            await db.Database.ExecuteSqlRawAsync("DELETE FROM quotes WHERE tenant_id = 1");
            Assert.Equal(0L, await Scalar<long>(db, "SELECT count(*) FROM quote_status_events"));

            // Rolling back would merge two companies: refused until only one is left.
            var refused = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(BeforeMultiTenancy));
            Assert.Contains("more than one tenant", refused.MessageText);

            await db.Database.ExecuteSqlRawAsync("""
                DELETE FROM quotes WHERE tenant_id <> 1; DELETE FROM clients WHERE tenant_id <> 1;
                DELETE FROM stores WHERE tenant_id <> 1; DELETE FROM tenants WHERE id <> 1;
                """);
            await migrator.MigrateAsync(BeforeMultiTenancy);

            Assert.Equal("Admin,Sales", await Scalar<string>(db, "SELECT string_agg(role, ',' ORDER BY role) FROM users"));
            Assert.Equal(0L, await Scalar<long>(db,
                "SELECT count(*) FROM information_schema.columns WHERE table_schema = 'public' AND column_name IN ('tenant_id', 'store_id')"));
            Assert.Equal(0L, await Scalar<long>(db,
                "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name IN ('tenants', 'stores', 'tenant_memberships', 'store_access')"));

            // And forward again, on the rolled-back schema.
            await db.Database.MigrateAsync();
            Assert.Equal(2L, await Scalar<long>(db, "SELECT count(*) FROM tenant_memberships"));
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    /// <summary>
    /// The policies exist only where Supabase's <c>auth</c> schema and <c>authenticated</c> role do, so
    /// the scratch database gets stand-ins for both before migrating, and each query then runs as
    /// <c>authenticated</c> with the caller's id in <c>request.jwt.claim.sub</c> — the way the Data API
    /// runs it.
    /// </summary>
    [Fact]
    public async Task Row_level_security_isolates_tenants_and_stores()
    {
        if (Server is null) return;

        var alfaAdmin = Guid.NewGuid();   // tenant 1, Admin, every store
        var alfaClerk = Guid.NewGuid();   // tenant 1, Sales, store 1 only
        var alfaStock = Guid.NewGuid();   // tenant 1, Warehouse, every store
        var betaSales = Guid.NewGuid();   // tenant 2, Sales
        var stranger = Guid.NewGuid();    // signed in, no membership
        var disabled = Guid.NewGuid();    // tenant 1 Admin, profile disabled

        await using var db = Open($"hvac_erp_rls_{Guid.NewGuid():N}", FixedTenantContext.System);
        try
        {
            await CreateDatabaseWithSupabaseStandInsAsync(db);
            await db.Database.MigrateAsync();

            await db.Database.ExecuteSqlRawAsync($"""
                INSERT INTO tenants (id, name, is_active, created_at) VALUES (2, 'Beta', true, now());
                INSERT INTO stores (id, tenant_id, name, is_active, created_at) VALUES (2, 1, 'Alfa north', true, now()), (3, 2, 'Beta', true, now());
                INSERT INTO users (id, email, display_name, created_at, is_active) VALUES
                    ('{alfaAdmin}', 'a@x', 'a', now(), true), ('{alfaClerk}', 'c@x', 'c', now(), true), ('{alfaStock}', 'w@x', 'w', now(), true),
                    ('{betaSales}', 'b@x', 'b', now(), true), ('{stranger}', 's@x', 's', now(), true), ('{disabled}', 'd@x', 'd', now(), false);
                INSERT INTO tenant_memberships (tenant_id, user_id, role, all_stores, created_at) VALUES
                    (1, '{alfaAdmin}', 'Admin', true, now()), (1, '{alfaClerk}', 'Sales', false, now()), (1, '{alfaStock}', 'Warehouse', true, now()),
                    (2, '{betaSales}', 'Sales', true, now()), (1, '{disabled}', 'Admin', true, now());
                INSERT INTO store_access (tenant_membership_id, store_id, tenant_id)
                SELECT id, 1, 1 FROM tenant_memberships WHERE user_id = '{alfaClerk}';
                INSERT INTO clients (tenant_id, code, full_name) VALUES (1, 'C1', 'Alfa client'), (2, 'C1', 'Beta client');
                INSERT INTO quotes (tenant_id, store_id, number, version, revision, client_id, department, created_at, predisposed, vat,
                    manual_discount, machines_price, installation_price, rounding, final_price, current_status)
                SELECT c.tenant_id, s.id, 1, 1, 0, c.id, 'Store', now(), false, 'Standard22', 0, 0, 0, 0, 0, 'Draft'
                FROM clients c JOIN stores s ON s.tenant_id = c.tenant_id;
                INSERT INTO quote_status_events (tenant_id, quote_id, status, occurred_at) SELECT tenant_id, id, 'Draft', now() FROM quotes;
                """);

            // Tenants: each side sees its own company and nothing of the other.
            Assert.Equal("Alfa client", await As<string>(db, alfaAdmin, "SELECT string_agg(full_name, ',') FROM clients"));
            Assert.Equal("Beta client", await As<string>(db, betaSales, "SELECT string_agg(full_name, ',') FROM clients"));
            Assert.Equal(2L, await As<long>(db, alfaAdmin, "SELECT count(*) FROM quotes"));
            Assert.Equal(1L, await As<long>(db, betaSales, "SELECT count(*) FROM quotes"));
            Assert.Equal(1L, await As<long>(db, betaSales, "SELECT count(*) FROM tenants"));
            Assert.Equal(1L, await As<long>(db, betaSales, "SELECT count(*) FROM tenant_memberships"));

            // Stores: the clerk of store 1 sees its quote and that quote's history, not store 2's.
            Assert.Equal("1", await As<string>(db, alfaClerk, "SELECT string_agg(store_id::text, ',') FROM quotes"));
            Assert.Equal(1L, await As<long>(db, alfaClerk, "SELECT count(*) FROM quote_status_events"));
            Assert.Equal(1L, await As<long>(db, alfaClerk, "SELECT count(*) FROM clients")); // clients are the tenant's

            // No membership, or a disabled profile: nothing, not even the shared lookups.
            foreach (var nobody in new[] { stranger, disabled })
            {
                Assert.Equal(0L, await As<long>(db, nobody, """
                    SELECT (SELECT count(*) FROM clients) + (SELECT count(*) FROM quotes) + (SELECT count(*) FROM products)
                         + (SELECT count(*) FROM vat_options) + (SELECT count(*) FROM tenants) + (SELECT count(*) FROM portal_brandings)
                         + (SELECT count(*) FROM tenant_memberships WHERE user_id <> auth.uid())
                    """));
            }
            Assert.Equal(1L, await As<long>(db, stranger, "SELECT count(*) FROM users")); // their own profile only

            // Writes: none across tenants, none above the role, none into a store one does not work in.
            Assert.Equal(0, await ExecuteAs(db, betaSales, "UPDATE clients SET full_name = 'changed' WHERE tenant_id = 1"));
            await AssertDeniedAs(db, betaSales, "INSERT INTO clients (tenant_id, code, full_name) VALUES (1, 'X', 'intruder')");
            await AssertDeniedAs(db, betaSales, $"INSERT INTO tenant_memberships (tenant_id, user_id, role, all_stores, created_at) VALUES (1, '{betaSales}', 'Admin', true, now())");
            Assert.Equal(0, await ExecuteAs(db, betaSales, $"UPDATE users SET is_platform_admin = true WHERE id = '{betaSales}'"));
            await AssertDeniedAs(db, alfaStock, "INSERT INTO clients (tenant_id, code, full_name) VALUES (1, 'W', 'by the warehouse')");
            Assert.Equal(0, await ExecuteAs(db, alfaClerk, "UPDATE quotes SET manual_discount = 1 WHERE store_id = 2"));
            Assert.Equal(1, await ExecuteAs(db, alfaClerk, "UPDATE quotes SET manual_discount = 1 WHERE store_id = 1"));
            Assert.Equal(0, await ExecuteAs(db, alfaClerk, "DELETE FROM clients")); // master data: Admin only
            Assert.Equal(1, await ExecuteAs(db, alfaClerk, "UPDATE clients SET full_name = 'Alfa client (edited)'"));
            await AssertDeniedAs(db, alfaAdmin, "INSERT INTO vat_options (code, label, sort_order, active, blend_shape, machines_multiplier, installation_multiplier) VALUES ('X', 'X', 9, true, 'None', 1, 1)");
            Assert.Equal(0, await ExecuteAs(db, alfaAdmin, "UPDATE quote_status_events SET notes = 'rewritten'")); // history is append-only

            // The tenant's Admin manages its own members; anonymous callers get nothing at all.
            Assert.Equal(4L, await As<long>(db, alfaAdmin, "SELECT count(*) FROM tenant_memberships"));
            Assert.Equal(4L, await As<long>(db, alfaAdmin, "SELECT count(*) FROM users"));
            Assert.Equal(0L, await As<long>(db, null, "SELECT (SELECT count(*) FROM clients) + (SELECT count(*) FROM tenants) + (SELECT count(*) FROM users)"));
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    /// <summary>The <c>auth.uid()</c> function, roles and grants a Supabase project provides out of the box.</summary>
    private static async Task CreateDatabaseWithSupabaseStandInsAsync(AppDbContext db)
    {
        var builder = new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString());
        var database = builder.Database!;
        builder.Database = "postgres";
        await using (var admin = new NpgsqlConnection(builder.ConnectionString))
        {
            await admin.OpenAsync();
            await using var command = admin.CreateCommand();
            command.CommandText = """
                DO $do$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'authenticated') THEN CREATE ROLE authenticated NOLOGIN; END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'anon') THEN CREATE ROLE anon NOLOGIN; END IF;
                END $do$;
                """;
            await command.ExecuteNonQueryAsync();

            // CREATE DATABASE cannot share a batch with anything else.
            command.CommandText = $"CREATE DATABASE \"{database}\"";
            await command.ExecuteNonQueryAsync();
        }

        await db.Database.ExecuteSqlRawAsync("""
            CREATE SCHEMA auth;
            CREATE FUNCTION auth.uid() RETURNS uuid LANGUAGE sql STABLE
                AS $fn$ SELECT nullif(current_setting('request.jwt.claim.sub', true), '')::uuid $fn$;
            GRANT USAGE ON SCHEMA auth TO authenticated, anon;
            GRANT USAGE ON SCHEMA public TO authenticated, anon;
            ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO authenticated, anon;
            ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO authenticated, anon;
            """);
    }

    private static async Task<T> Scalar<T>(AppDbContext db, string sql) =>
        (T)(await RunAsync(db, null, false, sql, c => c.ExecuteScalarAsync()))!;

    /// <summary>A query as the Data API would run it for <paramref name="user"/> (null: anonymous).</summary>
    private static async Task<T> As<T>(AppDbContext db, Guid? user, string sql) =>
        (T)(await RunAsync(db, user, true, sql, c => c.ExecuteScalarAsync()))!;

    private static async Task<int> ExecuteAs(AppDbContext db, Guid? user, string sql) =>
        (int)(await RunAsync(db, user, true, sql, async c => await c.ExecuteNonQueryAsync()))!;

    private static async Task AssertDeniedAs(AppDbContext db, Guid user, string sql)
    {
        var error = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAs(db, user, sql));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
        Assert.Contains("row-level security", error.MessageText);
    }

    private static async Task AssertRejected(AppDbContext db, string sqlState, string constraintOrColumn, string sql)
    {
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql));
        Assert.Equal(sqlState, error.SqlState);
        Assert.Contains(constraintOrColumn, error.ConstraintName ?? error.ColumnName ?? error.MessageText);
    }

    private static async Task<object?> RunAsync(
        AppDbContext db, Guid? user, bool asDataApi, string sql, Func<DbCommand, Task<object?>> run)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        if (asDataApi)
        {
            command.CommandText = user is null
                ? "SET LOCAL ROLE anon"
                : $"SET LOCAL ROLE authenticated; SELECT set_config('request.jwt.claim.sub', '{user}', true)";
            await command.ExecuteNonQueryAsync();
        }

        command.CommandText = sql;
        try
        {
            var result = await run(command);
            await transaction.CommitAsync();
            return result;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
