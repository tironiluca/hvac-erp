# HVAC ERP — Architecture and conventions

The [README](../README.md) describes what each part does; this file holds the **rules** every change
follows and the **design decisions** behind the data model. Progress and plans are in
[ROADMAP.md](ROADMAP.md).

## Layers

| Project | Responsibility | Depends on |
|---|---|---|
| `HvacErp.Domain` | Entities, enums, lookup-code constants. No framework dependencies. | — |
| `HvacErp.Application` | DTOs, service interfaces + implementations, business logic (pricing engine, quote numbering, workflow transitions, incentive schemes, ERP export), PDF template model, ports (`ICurrentUser`, `IInstallationCalendarNotifier`, `IFileStorageService`, `IErpConnector`, document generators). | Domain |
| `HvacErp.Infrastructure` | EF Core (Npgsql, snake_case) + configurations + migrations + seeders, Supabase Storage client, QuestPDF generators, Supabase JWT claims transformation, ERP connectors and worker, mock ERP. Implements Application ports. | Application, Domain |
| `HvacErp.Api` | Controllers, DI composition root, JWT bearer auth, role policies, SignalR hub, `ApiExceptionHandler`, `CurrentUser`. | all of the above |

Frontend (`frontend/`, Angular 21): standalone components, SCSS, no SSR, zone-based change detection.
`core/` (guards, interceptors, models, services, shell, utils, dashboard), `features/<module>/`
(lazy-loaded areas with their own `*.routes.ts`), `shared/` (reusable components such as the F-Gas
section and the floor-plan sketch). Transloco i18n, English default, Italian available; labels of
server-driven options are keyed by lookup `code`. The Material 3 theme is generated at runtime from
the branding seeds.

## Rules for all new code

- **Never send a date via `.toISOString()`.** All date columns are `timestamptz`;
  `AppDbContext.ConfigureConventions` installs a global UTC converter (Npgsql requires UTC kind). Use
  `DateTime.UtcNow` server-side and convert to local only for display. The frontend serialises
  date-only inputs with `toLocalDateTimeIso()` (`core/utils/date.util.ts`); a plain `toISOString()`
  shifts a picked date by a day in positive UTC offsets.
- **Business enums are lookup tables, not C# enums.** `RepairStatus`, `VatOption`, `DuctSystemType`,
  `QuoteDepartment`, `DaySlot`, `QuoteStatus` are tables (string PK `code` + `label` / `description`
  / `color` / `sort_order` / `active`) referenced by string FKs `ON DELETE RESTRICT`; code branches on
  `Domain/Enums/LookupCodes.cs` constants; `GET /api/lookups/*` exposes each. Only `UserRole`,
  `ProductRole`, `RepairType` and `PdfDocumentType` stay real enums.
- **Market rules are data.** A VAT change is a `vat_options` edit (`blend_shape` +
  machines / installation multipliers); an incentive, fee or warranty is an `incentive_schemes` row
  (see the README). No rate literals in the pricing engine.
- **Match on codes, never on display text.** Labels are translatable data; use `service_items.code`,
  `products.is_hydronic`, category codes, lookup codes.
- **No customer identity in the repository.** Brand names, addresses, hosts, project ids and live
  data stay in configuration and the database (branding, PDF templates, seeds a deployment runs).
  Defaults are neutral and English.
- **Domain exceptions map to HTTP in one place.** `ApiExceptionHandler`: `NotFoundException` → 404,
  `ConflictException` → 409, `BusinessRuleException` → 400, `ForbiddenException` → 403, Postgres
  check / foreign-key violations → 400. Throw the domain exception; don't set status codes in
  services.
- **Every mutating endpoint declares a role policy** (`Api/Authorization/Policies.cs`): business writes
  `SalesOrAdmin`, settings and client deletes `AdminOnly`; reads need only sign-in (a fallback policy
  requires it everywhere). Re-check role-dependent sub-operations in Application with
  `ICurrentUser.EnsureInRole`. `AuthorizationMatrixTests` fails on a mutating action without a policy.
- **Every new table ships with RLS in its own migration** (`ENABLE ROW LEVEL SECURITY` + policies
  mirroring the API rule via `app_private.current_app_role()`, using the `RowLevelSecuritySql`
  helper), and is added to the RLS coverage test. Supabase's Data API exposes `public` with the
  publishable key that ships in the SPA bundle, so a table without RLS is world-readable.
- **Migrations**: one per change, generated with `dotnet ef migrations add`; data moves in the same
  migration with a working `Down`; a Postgres test for any migration that transforms data. CI fails
  when the model changed without a migration (`has-pending-model-changes`).
- **Every public type and member has a `<summary>`** — enforced by `XmlDocumentationCoverageTests`,
  surfaced in Swagger.
- **Correlation ids**: the SPA sends `X-Correlation-Id`; logs, problem responses and Sentry events
  carry it. Never echo exception text on a 500.
- **Audit stamping** goes through `ICurrentUser` (e-mail, else the Supabase user id; null outside a
  request, e.g. seeding). Status-event history tables are append-only.
- **External calls go through ports and outboxes.** Anything sent to another system (ERP today)
  is written to an outbox table in the same transaction as the business change and sent by a worker
  with retries; the business flow never waits on the remote system.
- **Tests with every change**: Application tests for logic, API tests for endpoints and policies,
  Vitest for frontend services and components.

## Design decisions in the data model

- **Line items are keyed to their own parent.** `QuoteServiceItem` and `RepairServiceItem` each
  reference only their parent.
- **Unit selection is a join.** A quote's external / internal units are `QuoteProduct` rows with a
  `Role` discriminator, not fixed column slots.
- **Ducted outlets are child rows** (`DuctedUnitOutlet`: position, system type, vent count); pricing is
  summed per outlet.
- **Crew assignment is many-to-many** (`InstallationInstaller`); crews come from `Installer.Crew`.
- **Status is an append-only event history** (`QuoteStatusEvent`, `RepairStatusEvent`) with a
  denormalised current status on the parent.
- **`FGasRecord` parent FKs cascade** — a record is meaningless without its parent, and the XOR
  check between parents must hold.
- **Sketches are vector JSON** (`FloorPlanSketchJson`, validated by `FloorPlanSketchValidator`);
  `Quote.FloorPlanImageUrl` (raster) is unused.
- **Incentives are rule kinds plus parameters**, not columns: `QuoteAmount` (fixed € or % of the
  equipment list total, per quote) and `InvoiceDiscount` (threshold rates, rounding step, customer
  share).
- **ERP orders are snapshots.** The `ErpOrder` payload is frozen at confirmation in
  `erp_order_exports`, so a later edit never changes what was sent.
