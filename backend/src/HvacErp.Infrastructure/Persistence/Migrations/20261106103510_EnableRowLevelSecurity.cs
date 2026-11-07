using System.Text;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HvacErp.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Database-enforced authorization (defense in depth for the API's role policies).
    /// <para>
    /// Every table in <c>public</c> gets row-level security. Supabase's Data API (PostgREST) exposes
    /// <c>public</c> to the <c>anon</c> / <c>authenticated</c> roles with the publishable key that ships
    /// in the SPA bundle, so without RLS any table is reachable directly, bypassing the API. With RLS
    /// on and only the policies below, <c>anon</c> sees nothing and a signed-in user gets exactly the
    /// access the API grants their <see cref="Domain.Enums.UserRole"/>.
    /// </para>
    /// <para>
    /// The API itself is unaffected: it connects as the table owner (<c>postgres</c>), which RLS does
    /// not apply to (no <c>FORCE ROW LEVEL SECURITY</c>), and FK cascades bypass RLS by design.
    /// </para>
    /// <para>
    /// Role lookups go through <c>app_private.current_app_role()</c> — <c>SECURITY DEFINER</c> so the
    /// <c>users</c> policies do not recurse, in a schema PostgREST does not expose. The policy half runs
    /// only where Supabase's <c>auth</c> schema and roles exist, so a plain local Postgres still migrates.
    /// </para>
    /// </summary>
    public partial class EnableRowLevelSecurity : Migration
    {
        /// <summary>Catalogue/lookup data: any signed-in user reads, only Admin writes.</summary>
        private static readonly string[] ReferenceTables =
        [
            "client_sources", "day_slots", "duct_system_types", "extra_types", "installers", "operators",
            "price_list_discount_tiers", "product_price_lists", "product_types", "products",
            "quote_departments", "quote_statuses", "repair_statuses", "service_items", "vat_options",
        ];

        /// <summary>Business records: any signed-in user reads, Sales/Admin write (mirrors the SalesOrAdmin policy).</summary>
        private static readonly string[] BusinessTables =
        [
            "clients", "quotes", "quote_products", "ducted_units", "ducted_unit_outlets", "quote_service_items",
            "quote_extras", "quote_attachments", "repairs", "repair_service_items", "repair_schedule_entries",
            "f_gas_records", "scheduled_installations", "installation_installers",
        ];

        /// <summary>Append-only history: read + insert only, never updated or deleted directly.</summary>
        private static readonly string[] AppendOnlyTables = ["quote_status_events", "repair_status_events"];

        /// <summary>Portal settings: any signed-in user reads, only Admin writes (mirrors the AdminOnly policy).</summary>
        private static readonly string[] SettingsTables = ["portal_brandings", "pdf_templates"];

        private const string UsersTable = "users";
        private const string MigrationsHistoryTable = "__EFMigrationsHistory";

        private const string IsSalesOrAdmin = "(select app_private.current_app_role()) in ('Admin', 'Sales')";
        private const string IsAdmin = "(select app_private.current_app_role()) = 'Admin'";

        /// <summary>Every table this migration secures (asserted against the EF model by the test suite).</summary>
        internal static IEnumerable<string> AllTables =>
            ReferenceTables.Concat(BusinessTables).Concat(AppendOnlyTables).Concat(SettingsTables)
                .Append(UsersTable).Append(MigrationsHistoryTable);

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in AllTables)
            {
                migrationBuilder.Sql($"""ALTER TABLE public."{table}" ENABLE ROW LEVEL SECURITY;""");
            }

            var policies = new StringBuilder();
            policies.AppendLine("""
                CREATE SCHEMA IF NOT EXISTS app_private;
                REVOKE ALL ON SCHEMA app_private FROM PUBLIC;

                CREATE OR REPLACE FUNCTION app_private.current_app_role() RETURNS text
                    LANGUAGE sql STABLE SECURITY DEFINER SET search_path = ''
                AS $fn$
                    SELECT u.role FROM public.users u WHERE u.id = (SELECT auth.uid())
                $fn$;

                REVOKE ALL ON FUNCTION app_private.current_app_role() FROM PUBLIC;
                GRANT USAGE ON SCHEMA app_private TO authenticated;
                GRANT EXECUTE ON FUNCTION app_private.current_app_role() TO authenticated;
                """);

            foreach (var table in ReferenceTables.Concat(SettingsTables))
            {
                AppendReadPolicy(policies, table);
                AppendWritePolicies(policies, table, IsAdmin, "admin");
            }

            foreach (var table in BusinessTables)
            {
                AppendReadPolicy(policies, table);
                // Deleting a client (master data) is Admin-only, like DELETE /api/clients/{id}.
                AppendWritePolicies(policies, table, IsSalesOrAdmin, "sales_or_admin",
                    deletePredicate: table == "clients" ? IsAdmin : null);
            }

            foreach (var table in AppendOnlyTables)
            {
                AppendReadPolicy(policies, table);
                policies.AppendLine($"""
                    CREATE POLICY {table}_insert_sales_or_admin ON public."{table}"
                        FOR INSERT TO authenticated WITH CHECK ({IsSalesOrAdmin});
                    """);
            }

            // Users see their own profile; only Admin sees or changes the roster (roles are granted by Admin).
            policies.AppendLine($"""
                CREATE POLICY users_select_self_or_admin ON public."{UsersTable}"
                    FOR SELECT TO authenticated USING (id = (SELECT auth.uid()) OR {IsAdmin});
                """);
            AppendWritePolicies(policies, UsersTable, IsAdmin, "admin");

            // __EFMigrationsHistory: RLS on with no policies — invisible to anon/authenticated.

            migrationBuilder.Sql(OnSupabaseOnly(policies.ToString()));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var drops = new StringBuilder();
            foreach (var table in AllTables)
            {
                drops.AppendLine($"""
                    FOR pol IN SELECT policyname FROM pg_policies WHERE schemaname = 'public' AND tablename = '{table}' LOOP
                        EXECUTE format('DROP POLICY %I ON public.%I', pol.policyname, '{table}');
                    END LOOP;
                    """);
            }

            migrationBuilder.Sql($"""
                DO $do$
                DECLARE pol record;
                BEGIN
                {drops}
                END
                $do$;
                """);
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS app_private.current_app_role();");
            migrationBuilder.Sql("DROP SCHEMA IF EXISTS app_private;");

            foreach (var table in AllTables)
            {
                migrationBuilder.Sql($"""ALTER TABLE public."{table}" DISABLE ROW LEVEL SECURITY;""");
            }
        }

        private static void AppendReadPolicy(StringBuilder sql, string table) =>
            sql.AppendLine($"""
                CREATE POLICY {table}_select_authenticated ON public."{table}"
                    FOR SELECT TO authenticated USING (true);
                """);

        private static void AppendWritePolicies(
            StringBuilder sql, string table, string predicate, string suffix, string deletePredicate = null)
        {
            var deleteSuffix = deletePredicate is null ? suffix : "admin";
            sql.AppendLine($"""
                CREATE POLICY {table}_insert_{suffix} ON public."{table}"
                    FOR INSERT TO authenticated WITH CHECK ({predicate});
                CREATE POLICY {table}_update_{suffix} ON public."{table}"
                    FOR UPDATE TO authenticated USING ({predicate}) WITH CHECK ({predicate});
                CREATE POLICY {table}_delete_{deleteSuffix} ON public."{table}"
                    FOR DELETE TO authenticated USING ({deletePredicate ?? predicate});
                """);
        }

        /// <summary>
        /// Wraps the statements in a DO block that is a no-op unless the Supabase <c>auth</c> schema and
        /// <c>authenticated</c> role exist. Each statement is run through EXECUTE so nothing referencing
        /// <c>auth.uid()</c> / <c>authenticated</c> is even parsed on a plain Postgres.
        /// </summary>
        private static string OnSupabaseOnly(string statements)
        {
            var body = new StringBuilder();
            foreach (var statement in SplitStatements(statements))
            {
                body.AppendLine($"    EXECUTE $stmt${statement}$stmt$;");
            }

            return $"""
                DO $do$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = 'auth')
                       OR NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'authenticated') THEN
                        RAISE NOTICE 'Supabase auth schema/roles not found - RLS enabled without policies.';
                        RETURN;
                    END IF;
                {body}
                END
                $do$;
                """;
        }

        /// <summary>Splits on top-level semicolons, keeping the <c>$fn$ … $fn$</c> function body intact.</summary>
        private static IEnumerable<string> SplitStatements(string sql)
        {
            var current = new StringBuilder();
            var inFunctionBody = false;
            foreach (var line in sql.Split('\n'))
            {
                // An odd number of $fn$ tags on a line opens or closes the function body.
                if ((line.Split("$fn$").Length - 1) % 2 == 1)
                {
                    inFunctionBody = !inFunctionBody;
                }

                current.AppendLine(line.TrimEnd('\r'));
                if (!inFunctionBody && line.TrimEnd().EndsWith(';'))
                {
                    var statement = current.ToString().Trim().TrimEnd(';');
                    if (statement.Length > 0)
                    {
                        yield return statement;
                    }
                    current.Clear();
                }
            }

            var tail = current.ToString().Trim().TrimEnd(';');
            if (tail.Length > 0)
            {
                yield return tail;
            }
        }
    }
}
