using System.Text;
using Microsoft.EntityFrameworkCore.Migrations;
using static HvacErp.Infrastructure.Persistence.Migrations.RowLevelSecuritySql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HvacErp.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Tax incentives, fees, invoice discounts and per-line options become data: <c>incentive_schemes</c>
    /// holds each scheme with its calculation kind and parameters; <c>quote_incentives</c> and
    /// <c>quote_product_incentives</c> record what each quote and line carries. The quotes' ENEA, RAEE,
    /// "sconto in fattura" and warranty columns move into those rows (same amounts and acceptance) and
    /// are dropped; quote PDF templates get the matching pricing-summary options.
    /// </summary>
    public partial class IncentiveSchemes : Migration
    {
        /// <summary>The tables this migration creates; all get row-level security (checked by the test suite).</summary>
        internal static readonly string[] SecuredTables = ["incentive_schemes", "quote_incentives", "quote_product_incentives"];

        // Seeded in the deployment's language: Italian when the existing PDF templates are Italian
        // (set by EnglishDefaultsAndStableCodes), else the English product defaults.
        private const string SeedSchemesSql = """
            INSERT INTO incentive_schemes (code, label, description, kind, default_amount, percent, add_to_price,
                include_in_invoice_discount_basis, requires_vat, requires_confirmation, threshold, rate_below_threshold,
                rate_above_threshold, rounding_step, pdf_label, pdf_invoice_discount_note, sort_order, active)
            SELECT v.* FROM (VALUES
                ('ENERGY_TAX_DEDUCTION', 'Detrazione fiscale ENEA',
                    'Pratica ENEA per la detrazione del cliente; rientra nella base dello sconto in fattura.',
                    'QuoteAmount', NULL::numeric, NULL::numeric, false, true, false, false, NULL::numeric, NULL::numeric,
                    NULL::numeric, NULL::numeric, 'Idoneo a detrazione fiscale ENEA', 'Pratica ENEA compresa', 10, true),
                ('WEEE_TAKE_BACK', 'Ritiro RAEE', 'Ritiro e smaltimento dell''apparecchiatura sostituita.',
                    'QuoteAmount', NULL, NULL, false, false, false, false, NULL, NULL,
                    NULL, NULL, 'Ritiro e smaltimento RAEE incluso', NULL, 20, true),
                ('INVOICE_DISCOUNT_50', 'Sconto in fattura 50%',
                    'Totale maggiorato x1,3 sotto 2.000 € (x1,2 da 2.000 €), arrotondato ai 50 € successivi; il cliente paga il 50%.',
                    'InvoiceDiscount', NULL, 50, false, false, true, true, 2000, 1.3,
                    1.2, 50, 'SCONTO IN FATTURA', NULL, 30, true),
                ('ALL_INCLUSIVE_WARRANTY', 'Garanzia All Inclusive', 'Garanzia estesa per unità, all''8% del prezzo di listino.',
                    'LineAmount', NULL, 8, true, false, false, false, NULL, NULL,
                    NULL, NULL, NULL, 'Garanzia All Inclusive compresa', 40, true)
            ) AS v
            WHERE EXISTS (SELECT 1 FROM pdf_templates WHERE definition_json #>> '{page,language}' = 'it');

            INSERT INTO incentive_schemes (code, label, description, kind, default_amount, percent, add_to_price,
                include_in_invoice_discount_basis, requires_vat, requires_confirmation, threshold, rate_below_threshold,
                rate_above_threshold, rounding_step, pdf_label, pdf_invoice_discount_note, sort_order, active)
            SELECT v.* FROM (VALUES
                ('ENERGY_TAX_DEDUCTION', 'Energy-efficiency tax deduction',
                    'Paperwork fee for the customer''s tax deduction; counts towards the invoice-discount basis.',
                    'QuoteAmount', NULL::numeric, NULL::numeric, false, true, false, false, NULL::numeric, NULL::numeric,
                    NULL::numeric, NULL::numeric, 'Eligible for the energy-efficiency tax deduction', 'Tax-deduction paperwork included', 10, true),
                ('WEEE_TAKE_BACK', 'WEEE take-back', 'Collection and disposal of the replaced equipment.',
                    'QuoteAmount', NULL, NULL, false, false, false, false, NULL, NULL,
                    NULL, NULL, 'WEEE collection and disposal included', NULL, 20, true),
                ('INVOICE_DISCOUNT_50', '50% invoice discount',
                    'Total grossed up ×1.3 below €2,000 (×1.2 from €2,000), raised to the next €50; the customer pays 50%.',
                    'InvoiceDiscount', NULL, 50, false, false, true, true, 2000, 1.3,
                    1.2, 50, 'INVOICE DISCOUNT', NULL, 30, true),
                ('ALL_INCLUSIVE_WARRANTY', 'All-inclusive warranty', 'Extended warranty per unit, priced at 8% of the unit''s list price.',
                    'LineAmount', NULL, 8, true, false, false, false, NULL, NULL,
                    NULL, NULL, NULL, 'All-inclusive warranty included', 40, true)
            ) AS v
            WHERE NOT EXISTS (SELECT 1 FROM pdf_templates WHERE definition_json #>> '{page,language}' = 'it');
            """;

        private const string MoveDataSql = """
            INSERT INTO quote_incentives (quote_id, incentive_scheme_id, amount, confirmed)
            SELECT q.id, s.id, q.enea_price, false
            FROM quotes q JOIN incentive_schemes s ON s.code = 'ENERGY_TAX_DEDUCTION'
            WHERE q.enea_eligible;

            INSERT INTO quote_incentives (quote_id, incentive_scheme_id, amount, confirmed)
            SELECT q.id, s.id, q.raee_price, false
            FROM quotes q JOIN incentive_schemes s ON s.code = 'WEEE_TAKE_BACK'
            WHERE q.raee_eligible;

            INSERT INTO quote_incentives (quote_id, incentive_scheme_id, amount, confirmed)
            SELECT q.id, s.id, q.sf_price, q.sf_confirmed
            FROM quotes q JOIN incentive_schemes s ON s.code = 'INVOICE_DISCOUNT_50'
            WHERE q.sf_requested;

            INSERT INTO quote_product_incentives (quote_product_id, incentive_scheme_id, amount)
            SELECT qp.id, s.id, COALESCE(qp.warranty_price, 0)
            FROM quote_products qp JOIN incentive_schemes s ON s.code = 'ALL_INCLUSIVE_WARRANTY'
            WHERE qp.warranty;
            """;

        // Quote templates: the pricing-summary options become scheme-agnostic. showIncentives is on
        // when either old note was; showWarrantyNote is folded into the schemes' own PDF notes.
        private const string TemplateOptionsUpSql = """
            UPDATE pdf_templates t
            SET definition_json = jsonb_set(t.definition_json, '{sections}', (
                SELECT jsonb_agg(
                    CASE WHEN s.value ->> 'type' = 'quote.pricingSummary' THEN
                        jsonb_set(s.value, '{options}', jsonb_build_object(
                            'showIncentives', COALESCE((s.value -> 'options' ->> 'showIncentives')::boolean,
                                COALESCE((s.value -> 'options' ->> 'showEnea')::boolean, true)
                                OR COALESCE((s.value -> 'options' ->> 'showRaee')::boolean, true)),
                            'showInvoiceDiscount', COALESCE((s.value -> 'options' ->> 'showInvoiceDiscount')::boolean,
                                (s.value -> 'options' ->> 'showSfBlock')::boolean, true)))
                    ELSE s.value END
                    ORDER BY s.ordinality)
                FROM jsonb_array_elements(t.definition_json -> 'sections') WITH ORDINALITY AS s(value, ordinality)))
            WHERE t.document_type = 'Quote'
              AND jsonb_typeof(t.definition_json -> 'sections') = 'array'
              AND jsonb_array_length(t.definition_json -> 'sections') > 0;
            """;

        private const string RestoreDataSql = """
            UPDATE quotes q SET enea_eligible = true, enea_price = i.amount
            FROM quote_incentives i JOIN incentive_schemes s ON s.id = i.incentive_scheme_id AND s.code = 'ENERGY_TAX_DEDUCTION'
            WHERE i.quote_id = q.id;

            UPDATE quotes q SET raee_eligible = true, raee_price = i.amount
            FROM quote_incentives i JOIN incentive_schemes s ON s.id = i.incentive_scheme_id AND s.code = 'WEEE_TAKE_BACK'
            WHERE i.quote_id = q.id;

            UPDATE quotes q SET sf_requested = true, sf_price = i.amount, sf_confirmed = i.confirmed
            FROM quote_incentives i JOIN incentive_schemes s ON s.id = i.incentive_scheme_id AND s.kind = 'InvoiceDiscount'
            WHERE i.quote_id = q.id;

            UPDATE quote_products qp SET warranty = true, warranty_price = i.amount
            FROM quote_product_incentives i JOIN incentive_schemes s ON s.id = i.incentive_scheme_id AND s.code = 'ALL_INCLUSIVE_WARRANTY'
            WHERE i.quote_product_id = qp.id;
            """;

        private const string TemplateOptionsDownSql = """
            UPDATE pdf_templates t
            SET definition_json = jsonb_set(t.definition_json, '{sections}', (
                SELECT jsonb_agg(
                    CASE WHEN s.value ->> 'type' = 'quote.pricingSummary' THEN
                        jsonb_set(s.value, '{options}', jsonb_build_object(
                            'showEnea', COALESCE((s.value -> 'options' ->> 'showIncentives')::boolean, true),
                            'showRaee', COALESCE((s.value -> 'options' ->> 'showIncentives')::boolean, true),
                            'showSfBlock', COALESCE((s.value -> 'options' ->> 'showInvoiceDiscount')::boolean, true),
                            'showWarrantyNote', COALESCE((s.value -> 'options' ->> 'showInvoiceDiscount')::boolean, true)))
                    ELSE s.value END
                    ORDER BY s.ordinality)
                FROM jsonb_array_elements(t.definition_json -> 'sections') WITH ORDINALITY AS s(value, ordinality)))
            WHERE t.document_type = 'Quote'
              AND jsonb_typeof(t.definition_json -> 'sections') = 'array'
              AND jsonb_array_length(t.definition_json -> 'sections') > 0;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "incentive_schemes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    label = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    default_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    percent = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    add_to_price = table.Column<bool>(type: "boolean", nullable: false),
                    include_in_invoice_discount_basis = table.Column<bool>(type: "boolean", nullable: false),
                    requires_vat = table.Column<bool>(type: "boolean", nullable: false),
                    requires_confirmation = table.Column<bool>(type: "boolean", nullable: false),
                    threshold = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    rate_below_threshold = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    rate_above_threshold = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    rounding_step = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    pdf_label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    pdf_invoice_discount_note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_incentive_schemes", x => x.id);
                    table.CheckConstraint("ck_incentive_schemes_invoice_discount", "kind <> 'InvoiceDiscount' OR (threshold IS NOT NULL AND rate_below_threshold > 0 AND rate_above_threshold > 0 AND rounding_step > 0 AND percent > 0 AND percent <= 100)");
                    table.CheckConstraint("ck_incentive_schemes_kind", "kind IN ('QuoteAmount', 'LineAmount', 'InvoiceDiscount')");
                    table.CheckConstraint("ck_incentive_schemes_line_amount", "kind <> 'LineAmount' OR default_amount IS NOT NULL OR percent IS NOT NULL");
                });

            migrationBuilder.CreateTable(
                name: "quote_incentives",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    quote_id = table.Column<int>(type: "integer", nullable: false),
                    incentive_scheme_id = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    confirmed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quote_incentives", x => x.id);
                    table.ForeignKey(
                        name: "fk_quote_incentives_incentive_schemes_incentive_scheme_id",
                        column: x => x.incentive_scheme_id,
                        principalTable: "incentive_schemes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_quote_incentives_quotes_quote_id",
                        column: x => x.quote_id,
                        principalTable: "quotes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quote_product_incentives",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    quote_product_id = table.Column<int>(type: "integer", nullable: false),
                    incentive_scheme_id = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quote_product_incentives", x => x.id);
                    table.ForeignKey(
                        name: "fk_quote_product_incentives_incentive_schemes_incentive_scheme",
                        column: x => x.incentive_scheme_id,
                        principalTable: "incentive_schemes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_quote_product_incentives_quote_products_quote_product_id",
                        column: x => x.quote_product_id,
                        principalTable: "quote_products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_incentive_schemes_code",
                table: "incentive_schemes",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_quote_incentives_incentive_scheme_id",
                table: "quote_incentives",
                column: "incentive_scheme_id");

            migrationBuilder.CreateIndex(
                name: "ix_quote_incentives_quote_id_incentive_scheme_id",
                table: "quote_incentives",
                columns: new[] { "quote_id", "incentive_scheme_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_quote_product_incentives_incentive_scheme_id",
                table: "quote_product_incentives",
                column: "incentive_scheme_id");

            migrationBuilder.CreateIndex(
                name: "ix_quote_product_incentives_quote_product_id_incentive_scheme_",
                table: "quote_product_incentives",
                columns: new[] { "quote_product_id", "incentive_scheme_id" },
                unique: true);

            migrationBuilder.Sql(SeedSchemesSql);
            migrationBuilder.Sql(MoveDataSql);
            migrationBuilder.Sql(TemplateOptionsUpSql);

            foreach (var table in SecuredTables)
            {
                migrationBuilder.Sql(EnableFor(table));
            }

            var policies = new StringBuilder();
            AppendReadPolicy(policies, "incentive_schemes");
            AppendWritePolicies(policies, "incentive_schemes", IsAdmin, "admin");
            foreach (var table in new[] { "quote_incentives", "quote_product_incentives" })
            {
                AppendReadPolicy(policies, table);
                AppendWritePolicies(policies, table, IsSalesOrAdmin, "sales_or_admin");
            }
            migrationBuilder.Sql(OnSupabaseOnly(policies.ToString()));

            // The old columns go only once their data has moved.
            migrationBuilder.DropColumn(
                name: "enea_eligible",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "enea_price",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "raee_eligible",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "raee_price",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "sf_confirmed",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "sf_price",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "sf_requested",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "warranty",
                table: "quote_products");

            migrationBuilder.DropColumn(
                name: "warranty_price",
                table: "quote_products");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "enea_eligible",
                table: "quotes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "enea_price",
                table: "quotes",
                type: "numeric(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "raee_eligible",
                table: "quotes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "raee_price",
                table: "quotes",
                type: "numeric(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "sf_confirmed",
                table: "quotes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "sf_price",
                table: "quotes",
                type: "numeric(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "sf_requested",
                table: "quotes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "warranty",
                table: "quote_products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "warranty_price",
                table: "quote_products",
                type: "numeric(12,2)",
                nullable: true);

            migrationBuilder.Sql(RestoreDataSql);
            migrationBuilder.Sql(TemplateOptionsDownSql);

            migrationBuilder.DropTable(
                name: "quote_incentives");

            migrationBuilder.DropTable(
                name: "quote_product_incentives");

            migrationBuilder.DropTable(
                name: "incentive_schemes");
        }
    }
}
