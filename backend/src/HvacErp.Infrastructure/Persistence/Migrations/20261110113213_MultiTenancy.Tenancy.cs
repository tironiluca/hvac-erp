using System.Text;
using Microsoft.EntityFrameworkCore.Migrations;
using static HvacErp.Infrastructure.Persistence.Migrations.RowLevelSecuritySql;

namespace HvacErp.Infrastructure.Persistence.Migrations
{
    /// <content>
    /// The hand-written half of the migration: which table is owned how, the data move, the composite
    /// foreign keys and the tenant-aware row-level security. The table lists are asserted against the
    /// EF model by <c>TenancyCoverageTests</c> and <c>RowLevelSecurityCoverageTests</c>.
    /// </content>
    public partial class MultiTenancy
    {
        /// <summary>The default tenant and store (both id 1) that existing rows are moved to.</summary>
        private const int ExistingRowsOwner = 1;

        /// <summary>Tables created here; RLS is enabled on each (asserted by the coverage test).</summary>
        internal static readonly string[] SecuredTables = ["tenants", "stores", "tenant_memberships", "store_access"];

        /// <summary>A tenant's reference data: members read, the tenant's Admin writes.</summary>
        internal static readonly string[] CatalogueTables =
        [
            "installers", "operators", "client_sources", "product_types", "product_price_lists",
            "price_list_discount_tiers", "products", "service_items", "extra_types", "incentive_schemes", "pdf_templates",
        ];

        /// <summary>A tenant's master data, shared by its stores: Sales / Admin write, Admin deletes.</summary>
        internal static readonly string[] MasterDataTables = ["clients"];

        /// <summary>Records that live in one store (<c>tenant_id</c> + <c>store_id</c>).</summary>
        internal static readonly string[] StoreTables =
            ["quotes", "repairs", "scheduled_installations", "f_gas_records", "erp_order_exports"];

        /// <summary>Lines of a store record: they carry <c>tenant_id</c> and follow their parent row.</summary>
        internal static readonly (string Table, string Parent, string ParentKey)[] ChildTables =
        [
            ("quote_products", "quotes", "quote_id"),
            ("ducted_units", "quotes", "quote_id"),
            ("ducted_unit_outlets", "ducted_units", "ducted_unit_id"),
            ("quote_service_items", "quotes", "quote_id"),
            ("quote_extras", "quotes", "quote_id"),
            ("quote_incentives", "quotes", "quote_id"),
            ("quote_attachments", "quotes", "quote_id"),
            ("repair_service_items", "repairs", "repair_id"),
            ("repair_schedule_entries", "repairs", "repair_id"),
            ("installation_installers", "scheduled_installations", "scheduled_installation_id"),
        ];

        /// <summary>Status history of a store record: read and insert only.</summary>
        internal static readonly (string Table, string Parent, string ParentKey)[] AppendOnlyChildTables =
        [
            ("quote_status_events", "quotes", "quote_id"),
            ("repair_status_events", "repairs", "repair_id"),
        ];

        /// <summary>
        /// Shared by every tenant: the structural lookups the code branches on, and the portal branding
        /// until it becomes a per-tenant setting. Members read, a platform administrator writes.
        /// </summary>
        internal static readonly string[] PlatformTables =
        [
            "vat_options", "repair_statuses", "quote_statuses", "duct_system_types", "quote_departments", "day_slots",
            "portal_brandings",
        ];

        /// <summary>Every pre-existing table that gained a <c>tenant_id</c> column.</summary>
        internal static IEnumerable<string> TenantOwnedTables =>
            CatalogueTables.Concat(MasterDataTables).Concat(StoreTables)
                .Concat(ChildTables.Select(c => c.Table))
                .Concat(AppendOnlyChildTables.Select(c => c.Table));

        /// <summary>Every reference between two tenant-owned tables, with its composite foreign key.</summary>
        internal static readonly TenantForeignKey[] ForeignKeys =
        [
            new("clients", "referred_by_client_id", "clients", TenantDelete.Restrict),
            new("clients", "source_id", "client_sources", TenantDelete.SetNull),
            new("ducted_unit_outlets", "ducted_unit_id", "ducted_units", TenantDelete.Cascade),
            new("ducted_units", "quote_id", "quotes", TenantDelete.Cascade),
            new("erp_order_exports", "quote_id", "quotes", TenantDelete.Cascade),
            new("f_gas_records", "quote_id", "quotes", TenantDelete.Cascade),
            new("f_gas_records", "repair_id", "repairs", TenantDelete.Cascade),
            new("installation_installers", "installer_id", "installers", TenantDelete.Restrict),
            new("installation_installers", "scheduled_installation_id", "scheduled_installations", TenantDelete.Cascade),
            new("price_list_discount_tiers", "price_list_id", "product_price_lists", TenantDelete.Cascade),
            new("products", "price_list_id", "product_price_lists", TenantDelete.Restrict),
            new("quote_attachments", "quote_id", "quotes", TenantDelete.Cascade),
            new("quote_extras", "extra_type_id", "extra_types", TenantDelete.Restrict),
            new("quote_extras", "quote_id", "quotes", TenantDelete.Cascade),
            new("quote_incentives", "incentive_scheme_id", "incentive_schemes", TenantDelete.Restrict),
            new("quote_incentives", "quote_id", "quotes", TenantDelete.Cascade),
            new("quote_products", "product_id", "products", TenantDelete.Restrict),
            new("quote_products", "quote_id", "quotes", TenantDelete.Cascade),
            new("quote_service_items", "quote_id", "quotes", TenantDelete.Cascade),
            new("quote_service_items", "service_item_id", "service_items", TenantDelete.Restrict),
            new("quote_status_events", "quote_id", "quotes", TenantDelete.Cascade),
            new("quotes", "client_id", "clients", TenantDelete.Restrict),
            new("quotes", "operator_id", "operators", TenantDelete.SetNull),
            new("quotes", "parent_quote_id", "quotes", TenantDelete.Restrict),
            new("repair_schedule_entries", "installer_id", "installers", TenantDelete.SetNull),
            new("repair_schedule_entries", "repair_id", "repairs", TenantDelete.Cascade),
            new("repair_service_items", "repair_id", "repairs", TenantDelete.Cascade),
            new("repair_service_items", "service_item_id", "service_items", TenantDelete.Restrict),
            new("repair_status_events", "repair_id", "repairs", TenantDelete.Cascade),
            new("repairs", "client_id", "clients", TenantDelete.Restrict),
            new("repairs", "quote_id", "quotes", TenantDelete.SetNull),
            new("scheduled_installations", "quote_id", "quotes", TenantDelete.Restrict),
            new("service_items", "product_type_id", "product_types", TenantDelete.SetNull),
            new("store_access", "store_id", "stores", TenantDelete.Cascade),
            new("store_access", "tenant_membership_id", "tenant_memberships", TenantDelete.Cascade),
            new("tenant_memberships", "installer_id", "installers", TenantDelete.SetNull),
        ];

        /// <summary>Tables that carry <c>tenant_id</c> without an EF-managed key to <c>tenants</c>.</summary>
        private static IEnumerable<string> TablesNeedingTenantReference => TenantOwnedTables.Append("store_access");

        /// <summary>Tables targeted by a composite key, so they need <c>UNIQUE (tenant_id, id)</c>.</summary>
        private static IEnumerable<string> TablesNeedingTenantKey =>
            ForeignKeys.Select(k => k.Parent).Append("stores").Distinct().Order(StringComparer.Ordinal);

        // The default tenant takes the deployment's portal title as its name, so a branded
        // installation starts with its own company name; an unbranded one gets a neutral name.
        private const string BackfillSql = """
            INSERT INTO tenants (id, name, is_active, created_at)
            VALUES (1, COALESCE(NULLIF((SELECT portal_title FROM portal_brandings ORDER BY id LIMIT 1), ''), 'Default company'), true, now());
            INSERT INTO stores (id, tenant_id, name, is_active, created_at) VALUES (1, 1, 'Main store', true, now());
            SELECT setval(pg_get_serial_sequence('tenants', 'id'), 1);
            SELECT setval(pg_get_serial_sequence('stores', 'id'), 1);

            INSERT INTO tenant_memberships (tenant_id, user_id, role, all_stores, installer_id, created_at)
            SELECT 1, id, role, true, installer_id, now() FROM users;
            UPDATE users SET is_platform_admin = (role = 'Admin');
            """;

        private const string RefuseDownWithSeveralTenantsSql = """
            DO $do$
            BEGIN
                IF (SELECT count(*) FROM tenants) > 1 THEN
                    RAISE EXCEPTION 'MultiTenancy cannot be rolled back while more than one tenant exists: their data would be merged.';
                END IF;
            END
            $do$;
            """;

        // A profile without a membership had no access; the old schema has no way to say that, and
        // would recreate such a profile on its next sign-in anyway.
        private const string RestoreUserRolesSql = """
            UPDATE users u SET role = m.role, installer_id = m.installer_id
            FROM tenant_memberships m WHERE m.user_id = u.id;
            DELETE FROM users WHERE role = '';
            """;

        private const string LegacyRoleFunctionSql = """
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
            """;

        /// <summary>Every table that had policies before this migration.</summary>
        private static IEnumerable<string> LegacyPolicyTables =>
            EnableRowLevelSecurity.AllTables.Append("incentive_schemes").Append("quote_incentives").Append("erp_order_exports");

        /// <summary>Tenant / store references, the composite foreign keys, and no default owner for new rows.</summary>
        private static void ApplyTenancyConstraints(MigrationBuilder migrationBuilder)
        {
            foreach (var table in TenantOwnedTables)
            {
                migrationBuilder.Sql(TenancySql.DropDefault(table, "tenant_id"));
            }

            foreach (var table in StoreTables)
            {
                migrationBuilder.Sql(TenancySql.DropDefault(table, "store_id"));
            }

            foreach (var table in TablesNeedingTenantKey)
            {
                migrationBuilder.Sql(TenancySql.AddTenantKey(table));
            }

            foreach (var table in TablesNeedingTenantReference)
            {
                migrationBuilder.Sql(TenancySql.AddTenantReference(table));
            }

            foreach (var table in StoreTables)
            {
                migrationBuilder.Sql(TenancySql.AddStoreReference(table));
            }

            foreach (var key in ForeignKeys)
            {
                migrationBuilder.Sql(TenancySql.AddForeignKey(key));
            }
        }

        private static void RemoveTenancyConstraints(MigrationBuilder migrationBuilder)
        {
            foreach (var key in ForeignKeys)
            {
                migrationBuilder.Sql(TenancySql.DropConstraint(key.Table, key.ConstraintName));
            }

            foreach (var table in StoreTables)
            {
                migrationBuilder.Sql(TenancySql.DropConstraint(table, $"fk_{table}_store"));
            }

            foreach (var table in TablesNeedingTenantReference)
            {
                migrationBuilder.Sql(TenancySql.DropConstraint(table, $"fk_{table}_tenant"));
            }

            foreach (var table in TablesNeedingTenantKey)
            {
                migrationBuilder.Sql(TenancySql.DropTenantKey(table));
            }
        }

        /// <summary>Replaces the role-only policies with ones built on the caller's memberships.</summary>
        private static void ApplyTenantRowLevelSecurity(MigrationBuilder migrationBuilder)
        {
            foreach (var table in SecuredTables)
            {
                migrationBuilder.Sql(EnableFor(table));
            }

            migrationBuilder.Sql(DropAllPolicies(LegacyPolicyTables));
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS app_private.current_app_role();");

            var policies = new StringBuilder();
            policies.AppendLine(TenancySql.CreateFunctions);
            foreach (var table in CatalogueTables)
            {
                TenancySql.AppendPolicies(policies, table, TenantPolicyShape.Catalogue);
            }

            foreach (var table in MasterDataTables)
            {
                TenancySql.AppendPolicies(policies, table, TenantPolicyShape.MasterData);
            }

            foreach (var table in StoreTables)
            {
                TenancySql.AppendPolicies(policies, table, TenantPolicyShape.StoreRecord);
            }

            foreach (var (table, parent, parentKey) in ChildTables)
            {
                TenancySql.AppendPolicies(policies, table, TenantPolicyShape.Child, parent, parentKey);
            }

            foreach (var (table, parent, parentKey) in AppendOnlyChildTables)
            {
                TenancySql.AppendPolicies(policies, table, TenantPolicyShape.AppendOnlyChild, parent, parentKey);
            }

            foreach (var table in PlatformTables)
            {
                TenancySql.AppendPolicies(policies, table, TenantPolicyShape.Platform);
            }

            TenancySql.AppendTenancyTablePolicies(policies);

            // mock_erp_orders and __EFMigrationsHistory keep RLS on with no policy: invisible to the Data API.
            migrationBuilder.Sql(OnSupabaseOnly(policies.ToString()));
        }

        private static void RemoveTenantRowLevelSecurity(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DropAllPolicies(LegacyPolicyTables.Concat(SecuredTables)));
            migrationBuilder.Sql(TenancySql.DropFunctions);
        }

        /// <summary>The policies as they were before this migration (see the earlier RLS migrations).</summary>
        private static void RestoreLegacyRowLevelSecurity(MigrationBuilder migrationBuilder)
        {
            var policies = new StringBuilder();
            policies.AppendLine(LegacyRoleFunctionSql);

            foreach (var table in EnableRowLevelSecurity.ReferenceTables.Concat(EnableRowLevelSecurity.SettingsTables).Append("incentive_schemes"))
            {
                AppendReadPolicy(policies, table);
                AppendWritePolicies(policies, table, IsAdmin, "admin");
            }

            foreach (var table in EnableRowLevelSecurity.BusinessTables.Append("quote_incentives").Append("erp_order_exports"))
            {
                AppendReadPolicy(policies, table);
                AppendWritePolicies(policies, table, IsSalesOrAdmin, "sales_or_admin",
                    deletePredicate: table == "clients" ? IsAdmin : null);
            }

            foreach (var table in EnableRowLevelSecurity.AppendOnlyTables)
            {
                AppendReadPolicy(policies, table);
                policies.AppendLine($"""
                    CREATE POLICY {table}_insert_sales_or_admin ON public."{table}"
                        FOR INSERT TO authenticated WITH CHECK ({IsSalesOrAdmin});
                    """);
            }

            policies.AppendLine($"""
                CREATE POLICY users_select_self_or_admin ON public."users"
                    FOR SELECT TO authenticated USING (id = (SELECT auth.uid()) OR {IsAdmin});
                """);
            AppendWritePolicies(policies, "users", IsAdmin, "admin");

            migrationBuilder.Sql(OnSupabaseOnly(policies.ToString()));
        }
    }
}
