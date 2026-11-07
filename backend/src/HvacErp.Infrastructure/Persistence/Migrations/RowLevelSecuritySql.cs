using System.Text;

namespace HvacErp.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// SQL builders for the row-level-security migrations: the policy shapes (read for any signed-in
    /// user; writes for a role predicate) and the Supabase-only guard. Shared so a later migration
    /// that adds tables secures them exactly like <see cref="EnableRowLevelSecurity"/> did.
    /// </summary>
    internal static class RowLevelSecuritySql
    {
        public const string IsSalesOrAdmin = "(select app_private.current_app_role()) in ('Admin', 'Sales')";
        public const string IsAdmin = "(select app_private.current_app_role()) = 'Admin'";

        public static string EnableFor(string table) => $"""ALTER TABLE public."{table}" ENABLE ROW LEVEL SECURITY;""";

        public static string DisableFor(string table) => $"""ALTER TABLE public."{table}" DISABLE ROW LEVEL SECURITY;""";

        public static void AppendReadPolicy(StringBuilder sql, string table) =>
            sql.AppendLine($"""
                CREATE POLICY {table}_select_authenticated ON public."{table}"
                    FOR SELECT TO authenticated USING (true);
                """);

        public static void AppendWritePolicies(
            StringBuilder sql, string table, string predicate, string suffix, string? deletePredicate = null)
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

        /// <summary>A DO block that drops every policy on the given tables.</summary>
        public static string DropAllPolicies(IEnumerable<string> tables)
        {
            var drops = new StringBuilder();
            foreach (var table in tables)
            {
                drops.AppendLine($"""
                    FOR pol IN SELECT policyname FROM pg_policies WHERE schemaname = 'public' AND tablename = '{table}' LOOP
                        EXECUTE format('DROP POLICY %I ON public.%I', pol.policyname, '{table}');
                    END LOOP;
                    """);
            }

            return $"""
                DO $do$
                DECLARE pol record;
                BEGIN
                {drops}
                END
                $do$;
                """;
        }

        /// <summary>
        /// Wraps the statements in a DO block that is a no-op unless the Supabase <c>auth</c> schema and
        /// <c>authenticated</c> role exist. Each statement is run through EXECUTE so nothing referencing
        /// <c>auth.uid()</c> / <c>authenticated</c> is even parsed on a plain Postgres.
        /// </summary>
        public static string OnSupabaseOnly(string statements)
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
