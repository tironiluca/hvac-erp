# HVAC ERP — Roadmap and status

**This file is the source of truth for progress.** Checkboxes track outstanding work; resume from the
first unchecked box. What was delivered is summarised here; the detail lives in git history, the
[README](../README.md) and [ARCHITECTURE.md](ARCHITECTURE.md), not duplicated back into this file.

**Ground rules**

- Everything in the codebase — entities, services, endpoints, Angular symbols, seeds, defaults — is
  written in **English**. Other languages are translations (Transloco, PDF captions), chosen per
  deployment.
- **The product carries no customer identity.** A company's name, colours, logo, PDF footer, storage
  bucket, hosts and business labels are configuration and data, never code. Do not add a brand name,
  a customer address, a live host or a project id to this repository.
- Business rules that change with the market (VAT blends, incentives, price lists, lookups) are
  **data**, edited without a release.

---

## 1. Status at a glance

| Capability | Code | Notes |
|---|:---:|---|
| Auth & app shell | ✅ | Supabase Auth, roles, Material 3 shell, English / Italian UI |
| Clients | ✅ | List + create / edit, Admin delete |
| Quotes — wizard, pricing engine, PDF, attachments | ✅ | |
| Repairs & revisions | ✅ | |
| Installations — form + barcode PDF | ✅ | |
| F-Gas records | ✅ | |
| Schema normalisation, lookup tables, data-driven VAT | ✅ | |
| White-label portal branding | ✅ | |
| Real-time installation calendar (SignalR) | ✅ | |
| In-app PDF template editor | ✅ | |
| Free-hand floor-plan sketch | ✅ | |
| Engineering hardening — tests, CI, authz, RLS, docs, observability, backplane, dashboard | ✅ | |
| Company-agnostic product, English defaults | ✅ | §3 |
| Incentive schemes as data | ✅ | §4 — admin screen open |
| Main-ERP integration + mock ERP | ✅ | §5 — real adapter open |
| Multi-tenant and multi-store | ❌ | §6 |
| Statistics | ❌ | §7 |
| Proper data tables (server paging, sort, filters, export) | ❌ | §8 |
| Proper README, quick start and docs | ❌ | §9 |
| Warehouse / offers / staff leave modules | ❌ | §10 |
| Operations checklist for a deployment | — | §11 |
| Open product questions | — | §12 |

---

## 2. Delivered

Condensed, in delivery order.

- **Foundation & auth** — 4-project solution, EF Core + Npgsql snake_case, Supabase JWT validation
  + `AppUser` upsert, `authGuard` / `adminGuard`, Bearer interceptor scoped to `apiUrl`, Material
  toolbar / sidenav shell, i18n.
- **Clients** — searchable list + create / edit dialog; duplicate `Code` → 409.
- **Quotes** — routed wizard (client → external unit → internal units → ducted → extras → floor plan
  → summary), status-coloured list, QuestPDF customer document, Storage attachments, live client-side
  price + compatibility preview mirroring the server engine. Confirmed quotes lock.
- **Repairs & revisions** — single-page form, 10-state status, appointment scheduling, QuestPDF work
  order. A revision forces urgent = false / prepared = true.
- **Installations** — scheduling form (client → that client's confirmed quotes → date → morning /
  afternoon / full day → crew), QuestPDF field work order with a Code128 barcode and signature
  placeholders. The list survives at `/installations/list`.
- **F-Gas** — reusable `hvac-fgas-records-section` in the quote and repair views; append-only API.
- **Schema normalisation** — fixed-slot columns became child / lookup tables (ducted outlets with
  per-outlet pricing, `ExtraType` + `QuoteExtra`, price-list discount tiers applied at quote time,
  append-only status events + denormalised current status); business enums became lookup tables;
  VAT fully data-driven; DB CHECK / UNIQUE constraints.
- **White-label branding** — one-row `PortalBranding` (3 M3 seeds + title + logo / favicon /
  login background), `GET /api/branding` (anonymous, first paint), editor at `/settings/branding`
  with live preview; seeds → 51 `--mat-sys-*` tokens at bootstrap and into the PDF headers.
- **Real-time installation calendar** — drag-and-drop week (crew resource grid) / month calendar,
  live-synced over SignalR (`/hubs/installations`) with presence avatars;
  `PATCH /api/installations/{id}/schedule` with optimistic concurrency (stale row version → 409 →
  refetch).
- **PDF template editor** — admins edit sections, table columns, ordering, page setup, header /
  footer and free-text blocks of the three PDFs, with a server-rendered preview (`PdfTemplate`,
  `jsonb` definition, one active per type, per-template language).
- **Floor-plan sketch** — HTML5-canvas `hvac-floor-plan-sketch` (`ControlValueAccessor`) in the
  quote / repair / installation forms; multiple sheets, zoom + pan; stored as vector JSON, validated,
  rendered into the PDFs as SVG.
- **Engineering hardening** — xUnit suites (Application incl. a Postgres migration / query
  integration test, API with `WebApplicationFactory`) + Vitest; GitHub Actions CI with Postgres /
  Redis service containers, migration-drift check and Docker build; role policies on every
  mutation; RLS on every table; XML docs in Swagger; `/health` + `/health/live`, correlated JSON
  logs, opt-in Sentry; client / repair deletes; opt-in Redis backplane + presence; dashboard
  (`GET /api/dashboard`).

---

## 3. Company-agnostic product — done

- [x] Company data out of code: empty default PDF footer, neutral default title "HVAC ERP" and
  colour seeds, `@example.com` seed e-mails, storage bucket from configuration (default
  `documents`), `hvac-erp*` Redis prefixes.
- [x] Code identifiers: projects / namespaces `HvacErp.*`, Angular project `hvac-erp`, component
  prefix `hvac-`, localStorage keys `hvac-*` (old keys migrated by `migrateLegacyPrefs()` before
  bootstrap).
- [x] Frontend deployment settings from `HVAC_*` build variables (`scripts/write-env.mjs`); a
  production build fails when one is missing.
- [x] English business defaults: seeds, PDF text and captions, UI default language. Italian stays
  available (`HVAC_DEFAULT_LANGUAGE=it`, per-template PDF language). Values the code matched by text
  became codes: `service_items.code`, `products.is_hydronic`, categories `Outdoor` / `Indoor`
  (migration `EnglishDefaultsAndStableCodes`, tested against Italian-shaped data).

**Done when** a fresh deployment with only the default seeds shows no customer identity. Keep it
that way: review every change for brand names, hosts and project ids.

## 4. Incentive schemes as data — done

Tax incentives, fees, invoice discounts and options (an energy-efficiency deduction, equipment
take-back, a 50% invoice discount, an extended warranty) are `incentive_schemes` rows with a
calculation `kind` and its parameters (see the README's *Incentive schemes*).

- [x] Domain + migration `IncentiveSchemes` (data moved from the old columns), RLS, PDF options
  `showIncentives` / `showInvoiceDiscount`.
- [x] Pricing engine, quote service (validation, confirmation kept across saves), API, PDF, quote
  wizard card with live preview.
- [x] Every quote amount is a fixed € or a % of the equipment list total, chosen per quote; the
  warranty covers the whole quote and is charged with the equipment before VAT (`charge_before_vat`,
  migration `QuoteLevelWarranty`).
- [ ] Admin screen to edit `incentive_schemes` (today: SQL).

## 5. Main-ERP integration — push confirmed orders

Goal: when a quote is confirmed, its order reaches the company's main ERP without anyone retyping it,
and the connection is a deployment setting, not code.

**Design**
- **Contract** `ErpOrder` (Application): one versioned, ERP-neutral document per confirmed quote —
  header (number, revision, dates, agent, department, VAT option, currency), customer, installation
  site, lines (`Product` units with list / net price, `Service` items, `Extra` work, `Incentive`
  amounts, ducted outlets) and totals.
- **Port** `IErpConnector` (`Name`, `PushOrderAsync(ErpOrder) → ErpPushResult{Success,
  ErpOrderNumber, Error, Retryable}`), chosen at runtime from `Erp:Provider`: `None` (off), `Http`
  (JSON over HTTPS, `Erp:BaseUrl`, API-key header); later dedicated adapters (e.g. SAP Business One,
  Odoo, Danea, TeamSystem) behind the same port. Field mapping that differs per ERP lives in the
  adapter, never in the domain.
- **Outbox** `erp_order_exports`: confirming a quote enqueues it in the same transaction; a
  background worker sends due rows with back-off (1, 2, 4… minutes, capped at an hour) and gives up
  to `Failed` after 8 attempts. One export per quote; `externalReference = quote-{id}` lets the ERP
  deduplicate a retried push.
- **API / UI**: `GET/POST /api/quotes/{id}/erp-export`; the quote page shows status and order number
  with *Send to ERP* / *Retry*.
- **Mock ERP** at `/mock-erp` (`MockErp:Enabled`, API key): numbers orders `ERP-yyyy-nnnnnn`,
  deduplicates, optional `FailureRate`, lists received orders.

**Steps**
- [x] Contract + mapper, port, connector registry, `Http` connector.
- [x] Outbox (RLS), enqueue on confirm, worker with back-off, endpoints, quote UI.
- [x] Mock ERP endpoints + page (migration `ErpIntegration`; `mock_erp_orders` has RLS and no
  policies).
- [ ] End-to-end check on a deployment: a confirmed quote reaches the mock ERP on its own.
- [ ] Real ERP adapter once the target ERP and its API are known (§12: which ERP, which objects —
  order vs. offer vs. delivery note — and who owns customer / article codes).

## 6. Multi-tenant and multi-store

Goal: one installation serves several companies (tenants), each with one or more stores (branches /
shops). Users work in one or more stores of a tenant, see nothing of other tenants, and admins edit
tenants, stores and access in-app.

**Model**
- `tenants` (company identity: legal name, VAT number, branding, PDF templates, incentive schemes,
  price lists, numbering) and `stores` (belong to a tenant: name, address, contacts, optional
  branding / PDF-template overrides, own quote numbering series).
- Every business row carries `tenant_id`; rows that live in a store (quotes, repairs, installations,
  calendar crews, F-Gas records, ERP exports) also carry `store_id`. Clients and the catalogue are
  per tenant, shared by its stores (a store-level price list override is possible later).
- Access: `tenant_memberships` (user, tenant, role Admin / Sales / Warehouse) and `store_access`
  (user, store); a tenant Admin can be granted all stores. A platform super-admin manages tenants.

**Security (defence in depth)**
- The SPA sends the active tenant / store (switcher in the toolbar, remembered per user); the API
  checks it against the user's memberships on every request (`ICurrentUser.TenantId / StoreIds`).
- EF Core global query filters on `tenant_id` (and the allowed `store_id` set); a `SaveChanges`
  interceptor stamps tenant / store on inserts and rejects writes into another tenant.
- Postgres RLS: policies on every table compare `tenant_id` / `store_id` with the caller's
  memberships via `app_private` SECURITY DEFINER functions (same pattern as `current_app_role()`).
- Composite foreign keys `(tenant_id, id)` so a row can never reference another tenant's row;
  unique keys per tenant (`(tenant_id, code)`), quote numbers per store.
- Tests: an isolation suite calling every endpoint as a user of tenant A with tenant B's ids
  (expect 404), and the RLS coverage test extended to "every table has a tenant policy".

**Editing (in-app)** — super-admin: create / suspend tenants. Tenant admin: company data, stores,
users, roles and store access, per-store numbering and overrides.

**Steps**
- [ ] Schema: tenants, stores, memberships, store access; migration creating a default tenant and
  store for existing data, backfilling `tenant_id` / `store_id`, then making them NOT NULL.
- [ ] Request context + query filters + write interceptor + RLS policies + composite FKs.
- [ ] Tenant / store switcher, admin screens (tenants, stores, users & access).
- [ ] Per-tenant settings: branding, PDF templates, incentive schemes, ERP connection (§5).
- [ ] Isolation test suite; then onboard a second tenant.

## 7. Statistics

Goal: a statistics page per tenant / store and period that tells the business how it sells, plus a
few light-hearted "fun facts" to make it worth opening.

**Core metrics** (filters: period, store, agent, department, client source)
- **Quote → order conversion**: confirmed ÷ issued (count and value), by agent, store, client
  source, department, product brand and price range; trend by month.
- **Time to close**: median days from first draft to confirmation; share closed within 7 / 30 days.
- **Recall effectiveness**: conversion of recalled vs. not recalled quotes; recalls per confirmed
  quote. **Revisions before yes**: average revisions per confirmed quote.
- **Pipeline**: open quotes value, ageing buckets (0–7, 8–30, 31–90, 90+ days), expected value.
- **Average ticket**: average quote and order value, machines vs. installation share.
- **Incentive uptake**: invoice-discount share and acceptance, tax-deduction and warranty attach
  rates.
- **Operations**: confirmation → installation lead time, crew utilisation (booked half-days ÷
  available), repairs opened / closed and mean time to close, by status.
- **Products**: top units and brands by confirmed quantity and value; refrigerant gas mix.
- **Seasonality**: quotes and confirmations per week heatmap.

**Fun facts** (rotating cards) — "BTU installed this year", "busiest weekday", "record day",
"most-haggled quote", "fastest yes", "longest streak of days with a confirmed quote", "CO₂-friendly"
share of R32 / heat-pump units.

**Implementation** — `StatisticsService` with server-side SQL aggregations (window functions; a
nightly-refreshed materialized view if volumes grow), `GET /api/statistics?from&to&storeId…`, CSV
export; an Angular page with KPI tiles, trend and breakdown charts and the fun-facts strip, scoped by
the tenant / store context of §6. "Purchase" = confirmed quote; once §5 is live, also "accepted by
the ERP".

**Steps**
- [ ] Metric definitions agreed (§12: is "purchase" the confirmation, the deposit or the ERP order?),
  service + endpoint + tests on seeded demo data.
- [ ] Statistics page (tiles, charts, filters, CSV) and fun facts.

## 8. Proper data tables for every list

Goal: every list screen (clients, quotes, repairs, installations, and later incentive schemes, ERP
exports, tenants / stores) becomes a real data grid that scales with the data.

**Today**
- The four lists use `mat-table` with fixed columns and no sorting, paging or column filters; only
  clients has a search box.
- The API has no paging: `GET /api/quotes` and `GET /api/repairs` return every row, and
  `GET /api/clients` silently stops at the first 100 matches (`ClientService` `Take(100)`).

**Target behaviour** (one shared component)
- **Server-side paging, sorting and filtering**: page size 25 / 50 / 100 with a total count, sort on
  any sortable column, free-text search, per-column filters (status, department, date range, agent,
  store, amount range).
- **State in the URL** (`?page=2&sort=createdAt:desc&status=Confirmed`); the last view per list is
  remembered per user.
- **Columns**: show / hide and reorder, sticky header and first column, locale-formatted money and
  dates, status chips.
- **Rows**: click to open, a row actions menu (open, PDF, duplicate, delete where allowed), multi-
  select with bulk actions (e.g. send to ERP, export).
- **Export** of the filtered view to CSV / Excel.
- **Empty, loading and error states**, skeleton rows, keyboard navigation and screen-reader labels
  (WCAG AA).
- **Mobile**: below the tablet breakpoint the table becomes a card list with the same filters.

**Implementation**
- Backend: `PageRequest` (page, size ≤ 100, sort, search, filters) and `PagedResult<T>(Items, Total,
  Page, Size)` in Application; an `IQueryable` extension applying a whitelist of sortable /
  filterable fields per list (no dynamic SQL from user input); list endpoints return `PagedResult`.
  Indexes for the default sorts and common filters, checked with `EXPLAIN`.
- Frontend: a generic `hvac-data-table` (column definitions as config) on `mat-table` + `MatSort` +
  `MatPaginator`, with a `ListQuery` service synced with the router query params.
- Tests: paging / sorting / filter whitelists, API contract tests, the component in Vitest, one
  Playwright run per list.

**Steps**
- [ ] Backend paging contract + clients (removes the 100-row cap) + tests.
- [ ] `hvac-data-table` component and the clients list on it.
- [ ] Quotes, repairs, installations on the new contract and component; indexes.
- [ ] Column chooser, saved views, CSV / Excel export, bulk actions.
- [ ] Mobile card layout and accessibility pass.

## 9. A proper README, quick start and docs

Goal: the repository's front page explains what HVAC ERP is, shows it, and gets a newcomer from
clone to a running demo in minutes. Written for three readers: an HVAC business owner evaluating it,
a developer running it, an operator deploying it.

**Target structure**
- **Header**: one-line pitch, CI / licence / .NET / Angular badges, a hero screenshot or GIF.
- **Features** with a screenshot each; planned items from §5–§8 marked "planned".
- **Quick start**: `docker compose up` starting Postgres, Redis, the API (migrations applied, demo
  data seeded) and the SPA, with a demo login per role; then the manual path.
- **Concepts**: roles and what each can do; the quote lifecycle (Draft → SurveyDone → Confirmed /
  Recalled, revisions, ERP export); incentive kinds with a worked pricing example; a glossary
  (F-Gas, WEEE, split / multi-split, ducted…).
- **Deploy your own**: step by step Supabase, Render and Vercel, with a checklist and the
  first-admin bootstrap; a note on other hosts.
- **Configuration** grouped (required / optional / integrations) with defaults; **Architecture**
  diagram linking to [ARCHITECTURE.md](ARCHITECTURE.md); **Development** (layout, tests, migrations,
  how to add an ERP connector or an incentive kind); **Troubleshooting / FAQ** (paused Supabase
  project, RLS denials, CORS, missing native libraries in the container, free-tier cold starts).
- **Project**: this roadmap, `CONTRIBUTING.md`, `SECURITY.md`, `CHANGELOG.md`, licence.

**Implementation** — screenshots by a Playwright script on seeded demo data (`docs/screenshots/`),
`docker-compose.yml` + a `Demo:Seed` switch, `frontend/README.md` folded in, a markdown link check
in CI.

**Steps**
- [ ] Demo seed + `docker-compose.yml` quick start, verified from a clean clone.
- [ ] README rewrite with features, concepts, deploy-your-own.
- [ ] Screenshot script and images; badges; link check in CI.
- [ ] `CONTRIBUTING.md`, `SECURITY.md`, `CHANGELOG.md`.

## 10. Further modules

Roughly in priority order.

- **Warehouse / inventory** — the largest remaining module: product and client registries, stock
  load / consume, EAN generation and reconciliation, stock reports. Its own `features/warehouse/`
  area, entities and endpoints.
- **Product offers / promotions** — depends on warehouse stock data.
- **Staff planning and leave** — a calendar-style entry form over a new `leave` table; feeds crew
  availability in the installation calendar.
- **Integrations** (touch the warehouse and offers): client import (CSV / Excel), price-list import
  and export (Excel), an order-export interchange file, stock and client-registry lookups in an
  external system.
- **Support error report** — a support-facing error report on top of the structured logs and
  Sentry.

## 11. Operations checklist for a deployment

Not code — settings each deployment should have. See the README's *Deployment*.

- [ ] Supabase Auth **leaked password protection** on (Dashboard → Auth → Password security).
- [ ] Render health-check path `/health/live`.
- [ ] Optional: Sentry projects, `Sentry__Dsn` (API) and `HVAC_SENTRY_DSN` (SPA).
- [ ] Before running more than one API instance: Redis and `Redis__ConnectionString`.
- [ ] Persist ASP.NET data-protection keys (today they live in the container; only matters for
  anything that relies on them across restarts).
- [ ] Migrations are applied by hand (`dotnet ef database update` or the generated script); CI's
  deploy summary flags pending ones.
- [ ] Tidy the frontend files that fail `prettier --check`, then add the check to CI.

## 12. Open product questions

Each needs a business decision, not just code.

- **Fields with no home yet**: a surveyor distinct from the agent; a free-text quote memo and an
  "additional option" memo; an ad-hoc one-off priced line (today `ManualDiscount` + `Rounding` are
  the escape hatches for an off-catalogue amount).
- **Client code auto-sequence**: codes are entered by hand, guarded only by a unique index (409).
  Generate the next code per series, or keep manual entry?
- **Status transition rules are unenforced**: any quote / repair status change is allowed (the
  history is recorded, the legality is not). Define the state machines and enforce them in the
  workflow services, or confirm free transitions.
- **Delete permissions**: client deletes are Admin-only and refused while the client has quotes or
  repairs; repair and quote deletes are Sales / Admin. Confirm per deployment.
- **Placeholder prices**: ducted-accessory `service_items` are seeded with placeholder prices and the
  WEEE take-back amount is a default; a deployment sets real figures.
- **ERP target** (§5) and the **definition of "purchase"** for statistics (§7).
