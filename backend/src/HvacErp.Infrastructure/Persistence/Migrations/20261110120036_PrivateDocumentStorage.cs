using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HvacErp.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Generated PDFs and quote attachments stop being public files. Their columns held a permanent
    /// public URL; they now hold the object's <b>path</b> in the private documents bucket, and the API
    /// hands out a signed link that expires when someone who may read the record asks for one.
    /// <para>
    /// <c>Up</c> renames the four columns and strips the public prefix
    /// (<c>https://…/storage/v1/object/public/&lt;bucket&gt;/</c>) from the stored values. Apply it
    /// with the release, then make the documents bucket private — until then the old links still work.
    /// </para>
    /// <para>
    /// <c>Down</c> puts the URLs back, which needs the prefix that was stripped. The database does not
    /// know it, so it is read from the setting <c>hvac.storage_public_base</c> and the rollback refuses
    /// to run without it while any path is stored:
    /// <c>ALTER DATABASE … SET hvac.storage_public_base = 'https://&lt;project&gt;.supabase.co/storage/v1/object/public/&lt;bucket&gt;'</c>
    /// (new connections pick it up; <c>RESET</c> it afterwards). The bucket must be public again for
    /// those URLs to work.
    /// </para>
    /// </summary>
    public partial class PrivateDocumentStorage : Migration
    {
        /// <summary>Tables and columns that hold the path of a private document.</summary>
        internal static readonly (string Table, string Column, string LegacyColumn)[] DocumentColumns =
        [
            ("quotes", "pdf_path", "pdf_url"),
            ("repairs", "pdf_path", "pdf_url"),
            ("scheduled_installations", "report_pdf_path", "report_pdf_url"),
            ("quote_attachments", "file_path", "file_url"),
        ];

        private const string PublicPrefixPattern = "^https?://[^/]+/storage/v1/object/public/[^/]+/";

        private static string StripPublicPrefixSql()
        {
            var sql = new System.Text.StringBuilder();
            foreach (var (table, column, _) in DocumentColumns)
            {
                sql.AppendLine($"""
                    UPDATE {table} SET {column} = regexp_replace({column}, '{PublicPrefixPattern}', '')
                    WHERE {column} ~ '{PublicPrefixPattern}';
                    """);
            }
            return sql.ToString();
        }

        private static string RestorePublicUrlsSql()
        {
            // A value that is still an absolute URL was never one of ours (a placeholder): left alone.
            var pending = string.Join(" + ", DocumentColumns.Select(c =>
                $"(SELECT count(*) FROM {c.Table} WHERE {c.Column} IS NOT NULL AND {c.Column} !~ '^https?://')"));
            var updates = string.Concat(DocumentColumns.Select(c => $"""

                    UPDATE {c.Table} SET {c.Column} = base || '/' || {c.Column}
                    WHERE {c.Column} IS NOT NULL AND {c.Column} !~ '^https?://';
                """));
            return $"""
                DO $do$
                DECLARE
                    base text := NULLIF(rtrim(current_setting('hvac.storage_public_base', true), '/'), '');
                BEGIN
                    IF ({pending}) = 0 THEN
                        RETURN;
                    END IF;
                    IF base IS NULL THEN
                        RAISE EXCEPTION 'PrivateDocumentStorage cannot be rolled back without the public URL prefix of the documents bucket: set hvac.storage_public_base first (see the migration''s summary).';
                    END IF;
                {updates}
                END
                $do$;
                """;
        }

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "report_pdf_url",
                table: "scheduled_installations",
                newName: "report_pdf_path");

            migrationBuilder.RenameColumn(
                name: "pdf_url",
                table: "repairs",
                newName: "pdf_path");

            migrationBuilder.RenameColumn(
                name: "pdf_url",
                table: "quotes",
                newName: "pdf_path");

            migrationBuilder.RenameColumn(
                name: "file_url",
                table: "quote_attachments",
                newName: "file_path");

            migrationBuilder.Sql(StripPublicPrefixSql());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RestorePublicUrlsSql());

            migrationBuilder.RenameColumn(
                name: "report_pdf_path",
                table: "scheduled_installations",
                newName: "report_pdf_url");

            migrationBuilder.RenameColumn(
                name: "pdf_path",
                table: "repairs",
                newName: "pdf_url");

            migrationBuilder.RenameColumn(
                name: "pdf_path",
                table: "quotes",
                newName: "pdf_url");

            migrationBuilder.RenameColumn(
                name: "file_path",
                table: "quote_attachments",
                newName: "file_url");
        }
    }
}
