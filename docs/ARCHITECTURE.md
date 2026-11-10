# HVAC ERP — Architecture and conventions

The [README](../README.md) describes what each part does; this file holds the **rules** every change
follows and the **design decisions** behind the data model. Progress and plans are in
[ROADMAP.md](ROADMAP.md).

## Layers

| Project | Responsibility | Depends on |
|---|---|---|
| `HvacErp.Domain` | Entities, enums, lookup-code constants. No framework dependencies. | — |
| `HvacErp.Application` | DTOs, service interfaces + implementations, business logic (pricing engine, quote numbering, workflow transitions, incentive schemes, ERP export), PDF template model, ports (`ICurrentUser`, `ITenantContext`, `IInstallationCalendarNotifier`, `IFileStorageService`, `IErpConnector`, document generators). | Domain |
| `HvacErp.Infrastructure` | EF Core (Npgsql, snake_case) + configurations + migrations + seeders, tenant filters and write guard (`AppDbContext`), sign-in resolution (`TenantAccessResolver`), Supabase Storage client, QuestPDF generators, ERP connectors and worker, mock ERP. Implements Application ports. | Application, Domain |
| `HvacErp.Api` | Controllers, DI composition root, JWT bearer auth, role policies, SignalR hub, `ApiExceptionHandler`, `CurrentUser`, `RequestTenantContext`. | all of the above |

Frontend (`frontend/`, Angular 21): standalone components, SCSS, no SSR, zone-based change detection.
`core/` (guards, interceptors, models, services, shell, utils, dashboard), `features/<module>/`
(lazy-loaded areas with their own `*.routes.ts`), `shared/` (reusable components such as the F-Gas
section and the floor-plan sketch). Transloco i18n, English default, Italian available; labels of
server-driven options are keyed by lookup `code`. The Material 3 theme is generated at runtime from
the branding seeds.

## Tenancy

One installation serves several companies (**tenants**), each with one or more **stores**. A user
works in a tenant because a `tenant_memberships` row says so — with a role, and either every store
or a list of them. Nothing else grants access: signing in creates a profile and no more.

**What is owned by whom**

| Ownership | Marker | Tables |
|---|---|---|
| A store of a tenant | `IStoreOwned` (`tenant_id` + `store_id`) | quotes, repairs, scheduled installations, F-Gas records, ERP exports |
| A tenant | `ITenantOwned` (`tenant_id`) | clients, the catalogue (products, price lists, service items, extra types, installers, operators…), incentive schemes, PDF templates, and every line / history row of a store record |
| Everyone | — | the six structural lookups (statuses, VAT options, day slots, departments, duct types), portal branding, `users`, `tenants` |

**The request** carries `X-Tenant-Id` and `X-Store-Id` (query `tenant` / `store` for SignalR). Both
are optional: without them a user works in their first tenant across every store they may use.
`TenantAccessResolver` checks the selection against the memberships and writes the answer as
claims; a selection that cannot be honoured grants *nothing* — it never falls back to another
tenant. Roles are per tenant, so the role claim is the role in the tenant of this request.

**Three layers enforce it**, each sufficient on its own:

1. **`AppDbContext`** — a global query filter on every tenant-owned entity (tenant, and for
   store-owned ones the readable stores), and a save guard that stamps `tenant_id` / `store_id` on
   new rows and refuses a write outside the caller's tenant and stores. A context with no tenant
   reads no tenant-owned row and can insert none.
2. **Composite foreign keys** — every reference between two tenant-owned tables exists twice: the
   ordinary key EF manages, and `(tenant_id, x_id) → parent (tenant_id, id)` with the same delete
   action. A row cannot point at another tenant's row even if the application is wrong.
3. **Row-level security** — policies built on `app_private.tenant_role(tenant_id)` and
   `app_private.can_access_store(tenant_id, store_id)`; lines follow their parent row.

**Rules**

- **A new tenant-owned table**: implement `ITenantOwned` or `IStoreOwned` (the filter and the stamp
  follow from the interface), and in its migration add the `tenant_id` reference, the composite
  keys and the policies with the `TenancySql` helper. `TenancyCoverageTests` fails until the table
  and each of its references are listed.
- **Never filter or stamp by hand.** No `Where(x => x.TenantId == …)` and no `TenantId = …` in a
  service: the context does both. The exception is a record that must live in its *parent's* store
  rather than the caller's selected one (an installation, an F-Gas record, an ERP export): set
  `StoreId` from the parent.
- **Reach lines through their parent.** Line tables are filtered by tenant only; the store rule
  lives on the parent. Query `context.Quotes.Include(...)`, or put the parent in the predicate
  (`a.Quote.Id == quoteId`), never a bare `context.QuoteAttachments.Where(a => a.Id == …)`.
- **Code that needs the store** reads `context.Tenant` (`RequireStoreId()` gives the 400 "select a
  store" when a user works in several and chose none). Quote numbers run per store.
- **`IgnoreQueryFilters()` needs a replacement predicate** in the same query (the caller's user id,
  for "my memberships") and a comment saying why.
- **`AsSystem()`** is for code with no caller behind it — the outbox worker, sign-in resolution,
  seeding. It lives on `AppDbContext`, not on `IAppDbContext`, so Application services cannot
  reach it. Inside it nothing is stamped: every row written must name its tenant.
- **Shared tables stay shared deliberately.** A table that is not tenant-owned must be listed as
  such in a migration (`PlatformTables`), and only a platform administrator writes it. A platform
  administrator manages tenants and shared settings; reading a tenant's data still takes a
  membership.
- **SignalR**: send to `CalendarGroups.Store(...)` / `.Tenant(...)`, never to `Clients.All`.
- **Tests**: `TestDb.Create()` works as tenant 1 / store 1; pass a `FixedTenantContext` and a shared
  database name to look at the same data as another tenant.

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
  `SalesOrAdmin`, a tenant's settings and client deletes `AdminOnly`, settings shared by every
  tenant `PlatformAdmin`; reads need a membership (the default and fallback policy — being signed
  in is not enough). Re-check role-dependent sub-operations in Application with
  `ICurrentUser.EnsureInRole`. `AuthorizationMatrixTests` fails on a mutating action without a policy.
- **Every new table ships with RLS in its own migration** (`ENABLE ROW LEVEL SECURITY` + policies
  mirroring the API rule, built with the `TenancySql` helper — see *Tenancy*), and is added to the
  RLS coverage test. Supabase's Data API exposes `public` with the
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
- **A stored file is a path, never a URL.** Generated PDFs and attachments are private documents:
  the row keeps the object's path (`…Path` columns), and a browser reaches the file only through
  `IFileStorageService.CreateSignedUrlAsync` — a link that expires after `DocumentLinks.Lifetime`,
  issued by an endpoint that first loads the owning record through the tenant / store filters.
  Never put a document link in a list or detail DTO (say `HasPdf` and let the client ask), never
  log one, never store one. Only what is shown before sign-in (logo, favicon, login background)
  goes to the public bucket with `UploadPublicAsync`.
- **Uploads are identified by their bytes** (`UploadedFileInspector`), never by the content type
  the client sent; a storage object is deleted with the row that points at it.
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
- **Roles belong to memberships, not to users.** `tenant_memberships.role` is the role in that
  tenant; `users` only identifies the person (plus `is_active` and `is_platform_admin`).
- **Tenant isolation is not left to EF relationships.** The composite keys are plain SQL next to
  EF's own, because EF cannot express `ON DELETE SET NULL (column)` and a composite `SET NULL`
  would try to null the tenant.
- **Status is an append-only event history** (`QuoteStatusEvent`, `RepairStatusEvent`) with a
  denormalised current status on the parent.
- **`FGasRecord` parent FKs cascade** — a record is meaningless without its parent, and the XOR
  check between parents must hold.
- **Document columns hold storage paths** (`quotes.pdf_path`, `repairs.pdf_path`,
  `scheduled_installations.report_pdf_path`, `quote_attachments.file_path`). Branding columns
  (`portal_brandings.*_url`) hold real public URLs, because those assets are public.
- **Sketches are vector JSON** (`FloorPlanSketchJson`, validated by `FloorPlanSketchValidator`);
  `Quote.FloorPlanImageUrl` (raster) is unused.
- **Incentives are rule kinds plus parameters**, not columns: `QuoteAmount` (fixed € or % of the
  equipment list total, per quote) and `InvoiceDiscount` (threshold rates, rounding step, customer
  share).
- **ERP orders are snapshots.** The `ErpOrder` payload is frozen at confirmation in
  `erp_order_exports`, so a later edit never changes what was sent.
