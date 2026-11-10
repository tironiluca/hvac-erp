using System.Text;

namespace HvacErp.Infrastructure.Persistence.Migrations
{
    /// <summary>What the database does to a referencing row when its parent is deleted.</summary>
    internal enum TenantDelete
    {
        /// <summary>Refuse the delete.</summary>
        Restrict,

        /// <summary>Delete the referencing row.</summary>
        Cascade,

        /// <summary>Clear the reference (only the id column: the tenant column stays).</summary>
        SetNull,
    }

    /// <summary>
    /// A reference between two tenant-owned tables. The database enforces it twice: the ordinary
    /// foreign key EF Core manages, and a composite one on <c>(tenant_id, column)</c> that makes it
    /// impossible to point at another tenant's row.
    /// </summary>
    /// <param name="Table">The referencing table.</param>
    /// <param name="Column">Its foreign-key column.</param>
    /// <param name="Parent">The referenced table.</param>
    /// <param name="OnDelete">The same action as the ordinary foreign key, so the two never disagree.</param>
    internal sealed record TenantForeignKey(string Table, string Column, string Parent, TenantDelete OnDelete)
    {
        /// <summary>Name of the composite constraint.</summary>
        public string ConstraintName => $"fk_{Table}_{Column}_tenant";
    }

    /// <summary>How row-level security treats a table once the product is multi-tenant.</summary>
    internal enum TenantPolicyShape
    {
        /// <summary>A tenant's reference data: members read, the tenant's Admin writes.</summary>
        Catalogue,

        /// <summary>A tenant's master data: members read, Sales / Admin write, Admin deletes.</summary>
        MasterData,

        /// <summary>A store's records: readable and writable only by those who work in the store.</summary>
        StoreRecord,

        /// <summary>A line of a store record: follows its parent row.</summary>
        Child,

        /// <summary>History of a store record: read and insert only.</summary>
        AppendOnlyChild,

        /// <summary>Shared by every tenant: members read, a platform administrator writes.</summary>
        Platform,
    }

    /// <summary>
    /// SQL builders for the multi-tenancy migrations: tenant / store columns and their keys, the
    /// composite foreign keys, and tenant-aware row-level security. Shared so a later migration that
    /// adds a tenant-owned table secures it exactly like <see cref="MultiTenancy"/> did.
    /// </summary>
    internal static class TenancySql
    {
        private const string Member = "app_private.tenant_role(tenant_id) IS NOT NULL";
        private const string TenantAdmin = "app_private.tenant_role(tenant_id) = 'Admin'";
        private const string TenantSalesOrAdmin = "app_private.tenant_role(tenant_id) IN ('Admin', 'Sales')";
        private const string InStore = "app_private.can_access_store(tenant_id, store_id)";
        private const string AnyMember = "(SELECT app_private.is_member())";
        private const string PlatformAdmin = "(SELECT app_private.is_platform_admin())";

        /// <summary>
        /// The functions every tenant policy calls. <c>SECURITY DEFINER</c> so the policies on
        /// <c>users</c> / <c>tenant_memberships</c> do not recurse, in a schema the Data API does not
        /// expose. A disabled user or a suspended tenant resolves to no role at all.
        /// </summary>
        public const string CreateFunctions = """
            CREATE SCHEMA IF NOT EXISTS app_private;
            REVOKE ALL ON SCHEMA app_private FROM PUBLIC;
            GRANT USAGE ON SCHEMA app_private TO authenticated;
            CREATE OR REPLACE FUNCTION app_private.tenant_role(p_tenant integer) RETURNS text
                LANGUAGE sql STABLE SECURITY DEFINER SET search_path = ''
            AS $fn$
                SELECT m.role
                FROM public.tenant_memberships m
                JOIN public.users u ON u.id = m.user_id
                JOIN public.tenants t ON t.id = m.tenant_id
                WHERE m.user_id = (SELECT auth.uid()) AND m.tenant_id = p_tenant AND u.is_active AND t.is_active
            $fn$;
            CREATE OR REPLACE FUNCTION app_private.can_access_store(p_tenant integer, p_store integer) RETURNS boolean
                LANGUAGE sql STABLE SECURITY DEFINER SET search_path = ''
            AS $fn$
                SELECT EXISTS (
                    SELECT 1
                    FROM public.tenant_memberships m
                    JOIN public.users u ON u.id = m.user_id
                    JOIN public.tenants t ON t.id = m.tenant_id
                    WHERE m.user_id = (SELECT auth.uid()) AND m.tenant_id = p_tenant AND u.is_active AND t.is_active
                      AND (m.all_stores OR EXISTS (
                          SELECT 1 FROM public.store_access a
                          WHERE a.tenant_membership_id = m.id AND a.store_id = p_store)))
            $fn$;
            CREATE OR REPLACE FUNCTION app_private.is_member() RETURNS boolean
                LANGUAGE sql STABLE SECURITY DEFINER SET search_path = ''
            AS $fn$
                SELECT EXISTS (
                    SELECT 1
                    FROM public.tenant_memberships m
                    JOIN public.users u ON u.id = m.user_id
                    JOIN public.tenants t ON t.id = m.tenant_id
                    WHERE m.user_id = (SELECT auth.uid()) AND u.is_active AND t.is_active)
            $fn$;
            CREATE OR REPLACE FUNCTION app_private.is_platform_admin() RETURNS boolean
                LANGUAGE sql STABLE SECURITY DEFINER SET search_path = ''
            AS $fn$
                SELECT COALESCE((
                    SELECT u.is_platform_admin FROM public.users u
                    WHERE u.id = (SELECT auth.uid()) AND u.is_active), false)
            $fn$;
            REVOKE ALL ON FUNCTION app_private.tenant_role(integer) FROM PUBLIC;
            REVOKE ALL ON FUNCTION app_private.can_access_store(integer, integer) FROM PUBLIC;
            REVOKE ALL ON FUNCTION app_private.is_member() FROM PUBLIC;
            REVOKE ALL ON FUNCTION app_private.is_platform_admin() FROM PUBLIC;
            GRANT EXECUTE ON FUNCTION app_private.tenant_role(integer) TO authenticated;
            GRANT EXECUTE ON FUNCTION app_private.can_access_store(integer, integer) TO authenticated;
            GRANT EXECUTE ON FUNCTION app_private.is_member() TO authenticated;
            GRANT EXECUTE ON FUNCTION app_private.is_platform_admin() TO authenticated;
            """;

        /// <summary>Drops the functions of <see cref="CreateFunctions"/>.</summary>
        public const string DropFunctions = """
            DROP FUNCTION IF EXISTS app_private.tenant_role(integer);
            DROP FUNCTION IF EXISTS app_private.can_access_store(integer, integer);
            DROP FUNCTION IF EXISTS app_private.is_member();
            DROP FUNCTION IF EXISTS app_private.is_platform_admin();
            """;

        /// <summary><c>UNIQUE (tenant_id, id)</c> on a table, the target of the composite foreign keys.</summary>
        public static string AddTenantKey(string table) =>
            $"""ALTER TABLE public."{table}" ADD CONSTRAINT uq_{table}_tenant_id_id UNIQUE (tenant_id, id);""";

        /// <summary>Drops <see cref="AddTenantKey"/>.</summary>
        public static string DropTenantKey(string table) =>
            $"""ALTER TABLE public."{table}" DROP CONSTRAINT IF EXISTS uq_{table}_tenant_id_id;""";

        /// <summary><c>tenant_id</c> → <c>tenants</c>; a tenant with data cannot be deleted, only suspended.</summary>
        public static string AddTenantReference(string table) =>
            $"""
            ALTER TABLE public."{table}" ADD CONSTRAINT fk_{table}_tenant
                FOREIGN KEY (tenant_id) REFERENCES public.tenants (id) ON DELETE RESTRICT;
            """;

        /// <summary><c>(tenant_id, store_id)</c> → <c>stores</c>: the store belongs to the row's tenant.</summary>
        public static string AddStoreReference(string table) =>
            $"""
            ALTER TABLE public."{table}" ADD CONSTRAINT fk_{table}_store
                FOREIGN KEY (tenant_id, store_id) REFERENCES public.stores (tenant_id, id) ON DELETE RESTRICT;
            """;

        /// <summary>The composite foreign key of <paramref name="key"/>.</summary>
        public static string AddForeignKey(TenantForeignKey key)
        {
            var action = key.OnDelete switch
            {
                TenantDelete.Cascade => "CASCADE",
                // Postgres 15+: clear only the id; tenant_id is NOT NULL and must stay.
                TenantDelete.SetNull => $"SET NULL ({key.Column})",
                _ => "RESTRICT",
            };
            return $"""
                ALTER TABLE public."{key.Table}" ADD CONSTRAINT {key.ConstraintName}
                    FOREIGN KEY (tenant_id, {key.Column}) REFERENCES public."{key.Parent}" (tenant_id, id) ON DELETE {action};
                """;
        }

        /// <summary>Drops a constraint added by this class.</summary>
        public static string DropConstraint(string table, string constraint) =>
            $"""ALTER TABLE public."{table}" DROP CONSTRAINT IF EXISTS {constraint};""";

        /// <summary>A new row must say which tenant / store it belongs to: no database default.</summary>
        public static string DropDefault(string table, string column) =>
            $"""ALTER TABLE public."{table}" ALTER COLUMN {column} DROP DEFAULT;""";

        /// <summary>Appends the policies of a table. <paramref name="parent"/> and <paramref name="parentKey"/> are for the child shapes.</summary>
        public static void AppendPolicies(
            StringBuilder sql, string table, TenantPolicyShape shape, string? parent = null, string? parentKey = null)
        {
            // A child is visible exactly when its parent is: the sub-select runs under the caller's
            // own policies on the parent, so the store rule is inherited rather than repeated.
            var parentVisible = parent is null
                ? null
                : $"""EXISTS (SELECT 1 FROM public."{parent}" p WHERE p.id = "{table}".{parentKey})""";

            switch (shape)
            {
                case TenantPolicyShape.Catalogue:
                    Append(sql, table, select: Member, write: TenantAdmin, delete: TenantAdmin);
                    break;
                case TenantPolicyShape.MasterData:
                    Append(sql, table, select: Member, write: TenantSalesOrAdmin, delete: TenantAdmin);
                    break;
                case TenantPolicyShape.StoreRecord:
                    Append(sql, table, select: InStore, write: $"{TenantSalesOrAdmin} AND {InStore}", delete: $"{TenantSalesOrAdmin} AND {InStore}");
                    break;
                case TenantPolicyShape.Child:
                    Append(sql, table, select: $"{Member} AND {parentVisible}",
                        write: $"{TenantSalesOrAdmin} AND {parentVisible}", delete: $"{TenantSalesOrAdmin} AND {parentVisible}");
                    break;
                case TenantPolicyShape.AppendOnlyChild:
                    sql.AppendLine($"""
                        CREATE POLICY {table}_select ON public."{table}"
                            FOR SELECT TO authenticated USING ({Member} AND {parentVisible});
                        CREATE POLICY {table}_insert ON public."{table}"
                            FOR INSERT TO authenticated WITH CHECK ({TenantSalesOrAdmin} AND {parentVisible});
                        """);
                    break;
                case TenantPolicyShape.Platform:
                    Append(sql, table, select: AnyMember, write: PlatformAdmin, delete: PlatformAdmin);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(shape));
            }
        }

        /// <summary>
        /// Policies of the tenancy tables themselves. A person reads their own profile and grants; a
        /// tenant's Admin reads and edits the tenant's stores and members; only a platform
        /// administrator creates tenants or changes a profile (which carries the platform-admin flag).
        /// </summary>
        public static void AppendTenancyTablePolicies(StringBuilder sql)
        {
            Append(sql, "tenants",
                select: $"app_private.tenant_role(id) IS NOT NULL OR {PlatformAdmin}", write: PlatformAdmin, delete: PlatformAdmin);
            Append(sql, "stores", select: Member, write: TenantAdmin, delete: TenantAdmin);
            Append(sql, "tenant_memberships",
                select: $"user_id = (SELECT auth.uid()) OR {TenantAdmin}", write: TenantAdmin, delete: TenantAdmin);
            Append(sql, "store_access",
                select: """EXISTS (SELECT 1 FROM public.tenant_memberships m WHERE m.id = store_access.tenant_membership_id)""",
                write: TenantAdmin, delete: TenantAdmin);
            Append(sql, "users",
                select: $"""
                    id = (SELECT auth.uid()) OR {PlatformAdmin}
                    OR EXISTS (SELECT 1 FROM public.tenant_memberships m
                               WHERE m.user_id = users.id AND app_private.tenant_role(m.tenant_id) = 'Admin')
                    """,
                write: PlatformAdmin, delete: PlatformAdmin);
        }

        private static void Append(StringBuilder sql, string table, string select, string write, string delete) =>
            sql.AppendLine($"""
                CREATE POLICY {table}_select ON public."{table}"
                    FOR SELECT TO authenticated USING ({select});
                CREATE POLICY {table}_insert ON public."{table}"
                    FOR INSERT TO authenticated WITH CHECK ({write});
                CREATE POLICY {table}_update ON public."{table}"
                    FOR UPDATE TO authenticated USING ({write}) WITH CHECK ({write});
                CREATE POLICY {table}_delete ON public."{table}"
                    FOR DELETE TO authenticated USING ({delete});
                """);
    }
}
