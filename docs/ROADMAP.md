# HVAC ERP — Roadmap and status

**This file is the source of truth for progress.** Checkboxes track outstanding work; resume from the
first unchecked box of the next item in §1's *Suggested order*. What was delivered is summarised
here; the detail lives in git history, the [README](../README.md) and
[ARCHITECTURE.md](ARCHITECTURE.md), not duplicated back into this file.

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

✅ done · 🟡 partly done, the section says what is left · ❌ not started

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
| Company-agnostic product, English defaults | ✅ | §3 — one leftover |
| Incentive schemes as data | ✅ | §4 — admin screen in §12 |
| Main-ERP integration + mock ERP | ✅ | §5 — real adapter open |
| Multi-tenant and multi-store | 🟡 | §6 — model, isolation and switcher done; admin screens and per-tenant settings open |
| Statistics | ❌ | §7 |
| Proper data tables (server paging, sort, filters, export), global search | ❌ | §8 |
| Proper README, quick start and docs | ❌ | §9 |
| Offers / staff leave / customer portal / smaller modules | ❌ | §10 |
| Security and access hardening | 🟡 | §11 — access model, upload and delete guards done; private storage next |
| Administration: users, catalogue, reference data | ❌ | §12 — today: SQL |
| Quote lifecycle: state machine, follow-ups, acceptance, payments | ❌ | §13 |
| One field calendar: installations, repairs, surveys, conflicts | ❌ | §14 |
| Technician mobile app (PWA) | ❌ | §15 |
| Installed equipment, warranty, maintenance contracts | ❌ | §16 |
| F-Gas compliance register | ❌ | §17 — records exist, the register does not |
| Notifications and document archive | ❌ | §18 |
| Warehouse and purchasing | ❌ | §19 |
| Platform and quality | ❌ | §20 |
| Operations checklist for a deployment | — | §21 |
| Open product questions | — | §22 |

**Suggested order** (why this order, not just what)

1. **§11 Security and access hardening** — closes exposures that exist on a live deployment today.
   *Access model done with §6; next: private storage and signed URLs.*
2. **§5** end-to-end check — small, proves the outbox on a real deployment.
3. **§6 multi-tenancy** — decided yes; steps 1–2 and the switcher are done, so §13–§19 create
   their tables with `tenant_id` and a tenant policy from the start. *Next: tenant / store /
   member administration (with §12) and per-tenant settings.*
4. **§8 paging contract + §12 administration** — every later module needs real grids and in-app
   reference data; this also ends the "edit it in SQL" steps.
5. **§18 notification outbox** — §13, §14, §16 and §17 all send reminders through it.
6. **§13 quote lifecycle** → **§14 one calendar** → **§15 technician app** — sell, plan,
   execute.
7. **§16 equipment and maintenance** → **§17 F-Gas register** — the after-sales loop and
   recurring revenue; §17 hangs its records on §16's units.
8. **§7 statistics** — worth more once §13 records lost reasons, payments and job completion.
9. **§19 warehouse and purchasing**, then §10.

§9 (docs) and §20 (platform) are not on the critical path: take a step from them alongside any
item.

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
- **Multi-tenancy foundation** (§6) — tenants, stores, memberships; `tenant_id` / `store_id` on
  every business table; isolation enforced by the database context (query filters, write guard),
  by composite foreign keys and by membership-based RLS; access granted only by a membership;
  tenant / store switcher; calendar fan-out per store.

---

## 3. Company-agnostic product — done, one leftover

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

- [x] Leftover from the rename: the scaffold `.http` file in `backend/src/HvacErp.Api/` carried a
  pre-rename project name as its **file name** (text searches miss file names) — deleted. The name
  stays in git history; rewrite history only if that matters to the deployment's owner. When
  reviewing for identity, list file names too (`git ls-files`), not only contents.
- [ ] English leftovers: the quote PDF is uploaded as `preventivo-{number}-….pdf`
  (`QuotePdfService`); the README's calendar paragraph quotes Italian slot labels. Name files from
  the template language.

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
- Admin screen to edit `incentive_schemes` (today: SQL) — tracked in §12.

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
- [ ] Real ERP adapter once the target ERP and its API are known (§22: which ERP, which objects —
  order vs. offer vs. delivery note — and who owns customer / article codes).
- [ ] Before a second API instance: the worker reads due rows without a lock
  (`ErpExportService.ProcessDueAsync`), so two instances push the same order twice and rely on the
  ERP deduplicating by `externalReference`. Claim rows with `FOR UPDATE SKIP LOCKED` (§20 job
  runner).
- [ ] Alert when an export ends `Failed` (§18), and an admin list of exports with bulk retry (§8).
- [ ] Inbound: order status / invoice number back from the ERP (webhook or poll through the same
  port), shown on the quote; customer and article code mapping tables once ownership is decided.

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

**As built** (the rules are in [ARCHITECTURE.md](ARCHITECTURE.md), *Tenancy*)
- **Ownership**: `ITenantOwned` / `IStoreOwned` on the entities. Store-level: quotes, repairs,
  installations, F-Gas records, ERP exports. Their lines carry `tenant_id` and follow the parent.
  Clients, catalogue, incentive schemes and PDF templates are per tenant.
- **Shared on purpose**: the six structural lookups the code branches on (statuses, VAT options,
  day slots, departments, duct types), the portal branding, the mock ERP. Members read them, a
  platform administrator writes them.
- **Access**: a `tenant_memberships` row is the only grant (role per tenant; every store or a
  list). The role left the user profile. An identity seen for the first time gets a profile and
  nothing else. `X-Tenant-Id` / `X-Store-Id` select where a request works; a selection the user may
  not use grants nothing rather than falling back. `Bootstrap:AdminEmail` names the first
  administrator of a fresh deployment and is inert once one exists.
- **Enforcement, three layers**: `AppDbContext` (global query filters, a save guard that stamps
  and refuses, an explicit `AsSystem()` scope for workers and seeding — no tenant means nothing is
  read and nothing can be inserted); composite foreign keys `(tenant_id, …)` mirroring every
  reference between tenant-owned tables; RLS rewritten around memberships, tenant and store.
- **Numbering**: client codes and the like are unique per tenant; quote numbers run per store.
- **Platform administrator**: creates tenants and edits shared settings; it is not a pass into a
  tenant's data — that still takes a membership.
- **Migration `MultiTenancy`**: existing data → tenant 1 / store 1 (named after the portal
  title); each user's role → a membership there with every store; existing Admins → platform
  administrators. `Down` restores the old schema and policies and refuses once a second tenant
  exists. Needs Postgres 15+.

**Steps**
- [x] Schema: tenants, stores, memberships, store access; migration creating a default tenant and
  store for existing data and backfilling `tenant_id` / `store_id` (NOT NULL, no default).
- [x] Request context + query filters + write guard + RLS policies + composite FKs.
- [x] Tenant / store switcher (toolbar, remembered per browser), `GET /api/me` listing what the
  user can switch to, a no-access page for a signed-in user with no membership.
- [x] Isolation tests: through the services, through HTTP with real sign-in resolution, and on
  Postgres with the policies run as the Data API runs them; model-vs-migration coverage tests so a
  new table or reference cannot skip its tenant key, composite FK or policy.
- [ ] **Administration** (with §12): platform admin creates / suspends tenants; tenant Admin edits
  company data, stores, members, roles and store access. Until then these are SQL inserts.
- [ ] **Tenant provisioning**: a new tenant starts with an empty catalogue; creating one must seed
  what the pricing engine needs (the coded service items, extra types, default incentive schemes,
  PDF templates). Today: `dotnet run -- seed --tenant <id>`.
- [ ] **Per-tenant settings**: branding (the login page is anonymous, so the tenant has to come
  from the host name — §22), ERP connection per tenant (the worker already runs across tenants),
  per-store overrides of branding / PDF templates.
- [ ] Store shown as a column / filter in the lists when the user works in several (§8); installers
  and crews assignable to a store (today they are tenant-wide).
- [ ] Cache sign-in resolution for a few seconds per user (two indexed queries per request today).
- [ ] Onboard a second tenant on a deployment.

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
- [ ] Metric definitions agreed (§22: is "purchase" the confirmation, the deposit or the ERP order?),
  service + endpoint + tests on seeded demo data.
- [ ] Statistics page (tiles, charts, filters, CSV) and fun facts.
- [ ] Once their data exists: lost reasons and win rate by reason (§13), margin and discount depth
  (§13), cash collected vs. outstanding (§13), first-time-fix rate and time on site (§15),
  maintenance renewal rate and contract revenue (§16), stock turns (§19).

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
- [ ] **Global search** in the toolbar (`Ctrl+K`): one endpoint over clients (name, code, phone,
  tax code), quotes (number), repairs (id, description) and, later, unit serials (§16). Today the
  client search is `lower(col) LIKE '%term%'` over four columns (`ClientService.SearchAsync`) — a
  sequential scan; add `pg_trgm` GIN indexes in the same migration.
- [ ] Projection queries for the lists: `GetListAsync` in the quote and repair services loads full
  entities with `Include` and maps in memory; select the DTO in SQL when the paging contract lands.

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
- [ ] README rewrite with features, concepts, deploy-your-own. Fix while there: the README tells
  the operator to create a **public** bucket (goes with the private-storage step of §11).
- [ ] Screenshot script and images; badges; link check in CI.
- [ ] `CONTRIBUTING.md`, `SECURITY.md`, `CHANGELOG.md`.

## 10. Further modules

Smaller than §11–§19 or dependent on them. Roughly in priority order.

- **Staff planning and leave** — a calendar-style entry form over a new `leave` table; feeds crew
  availability and the conflict check in the field calendar (§14).
- **Incentive paperwork tracking** — schemes are priced today (§4) but the practice behind them is
  not followed: per scheme, a checklist of required documents and a deadline as data; per quote, the
  checklist's state, uploaded documents (§18 archive) and a reminder before the deadline.
- **Customer portal** — a signed-in area for the customer on top of the acceptance link of §13:
  their quotes and documents, appointment dates, unit list and warranty (§16), "book a maintenance
  visit", "report a fault" (creates a repair in `New`). Its own Supabase role and RLS policies.
- **Product offers / promotions** — time-boxed price or bundle rules on catalogue rows; stock-aware
  once §19 exists.
- **Imports and exchanges**: client import (CSV / Excel) with a dry run and duplicate detection,
  price-list import and export (§12), an order-export interchange file for ERPs with no API (§5), a
  hand-off of payments to accounting / e-invoicing when the main ERP does not issue invoices (§22).
- **Sizing helper** — room by room (area, height, exposure, insulation, climate zone → suggested
  capacity) in the quote wizard, filtering units by `products.max_btu`; coefficients are data.
- **Support error report** — a support-facing error report on top of the structured logs and
  Sentry, keyed by the correlation id the SPA already shows.

## 11. Security and access hardening — do first

Found by reading the code against a live-deployment threat model. Each item names where it lives.

**Findings**
- ✅ *Fixed with §6.* **Anyone who could sign in became Sales.** Sign-in inserted an `AppUser`
  with role `Sales` for every valid Supabase token it had not seen, and every RLS read policy was
  `USING (true)` for `authenticated`. With sign-ups enabled on the Supabase project, the
  publishable key in the SPA bundle was enough to register, read every table through the Data API
  and write through the API. Now a new identity gets a profile and no access, and every policy
  asks for a membership.
- **Customer documents are public and enumerable.** `SupabaseStorageService` returns
  `/object/public/…` URLs and the README asks for a public bucket. A quote PDF lives at
  `quotes/{id}/preventivo-{number}-v{version}r{revision}.pdf` — sequential ids, no secret — and
  carries name, address, tax code and prices. Work orders and repairs follow the same pattern.
- ✅ *Fixed.* **Uploads were not checked.** Quote attachments accepted any content type up to
  20 MB and stored the client-supplied type; deleting an attachment removed the row and left the
  file. Now the type is read from the bytes (photos and PDF only) and the file goes with the row.
- ✅ *Fixed.* **A quote could be deleted from under its order.** `QuoteService.DeleteAsync` had no
  guard; the ERP export row cascaded away with the quote, so an order the ERP held lost its trace
  here, and a scheduled installation blocked the delete only as a generic foreign-key 400.
- **F-Gas records can be deleted** (`DELETE /api/fgas-records/{id}`) although they are compliance
  evidence; §2 describes the API as append-only.
- **No rate limiting, HSTS or forwarded-header handling** in `Program.cs`; the anonymous endpoints
  (`/api/branding`, `/mock-erp`, `/health`) are the exposed surface.
- **No audit trail beyond status events**: who changed a price, a client's data or a role is not
  recorded; there is no way to export or erase one customer's personal data on request.
- **Quote numbers race**: `QuoteNumberingService` takes `MAX(number) + 1` in application code. Two
  simultaneous first quotes collide on the unique index and one user gets a 409 for a number they
  never typed.

**Design**
- **Access is granted, never assumed.** `users.status` (`Invited` / `Active` / `Disabled`); the
  claims transformation adds a role only for `Active` rows and no longer inserts. An unknown or
  disabled identity gets 403 on everything except `GET /api/me` (which says "no access yet"). RLS
  read policies become `current_app_role() IS NOT NULL` and `current_app_role()` returns null unless
  the row is `Active`. The first admin comes from `Bootstrap:AdminEmail`, honoured only while no
  admin exists.
- **Invitations**: an admin invites by e-mail through the Supabase Auth admin API (the API already
  holds the service-role key), choosing the role and the linked installer / operator. Login gains
  "forgot password" and optional TOTP; admins can be required to enrol.
- **Private storage**: bucket private, rows store the object **path**, the API hands out signed
  URLs that expire in minutes (`GET /api/quotes/{id}/pdf-url`, same for repairs, installations,
  attachments). Branding assets stay public under their own prefix or bucket. Migration: strip the
  public base URL from stored values, with a working `Down`.
- **Uploads**: content-type and extension whitelist (images, PDF), size cap, type sniffed from the
  bytes, never trusted from the header; storage object deleted with its row and when the parent is
  deleted.
- **Deletes with consequences become cancellations**: a quote that is confirmed, exported or
  scheduled is cancelled (§13), not deleted; an F-Gas record is voided by a correcting entry (§17).
- **Edge**: ASP.NET rate limiter (per user, tighter per IP on anonymous routes), HSTS, forwarded
  headers for the proxy, `MockErp:Enabled` refused in Production unless explicitly forced.
- **Audit and privacy**: `audit_entries` (who, when, entity, key, changed columns as `jsonb`)
  written by a `SaveChanges` interceptor for a whitelist of entities, append-only, Admin-read;
  `GET /api/clients/{id}/export` (everything held about one person) and an anonymise action that
  blanks identity fields while keeping the fiscal records the law requires.
- **Numbering**: a Postgres sequence for new quote numbers (per store once §6 lands); version and
  revision computed under a per-client advisory lock.

**Steps**
- [ ] §21 today, no code: sign-ups off; Data API off or `public` removed from its exposed schemas
  (the SPA uses Supabase for Auth and Storage only).
- [x] Access is granted, never assumed (done as part of §6, with memberships in place of a status
  column): no auto-provisioning, `Bootstrap:AdminEmail`, a member-only default policy, `users.is_active`
  to disable a person, RLS policies that give nothing without a membership; tests for the
  unknown-identity path at every layer.
- [ ] Invitations by e-mail, "forgot password", optional TOTP (with the users screen of §12).
- [ ] Private bucket, stored paths, signed-URL endpoints, SPA and PDF generators updated, data
  migration. **Needs a deployment step** (bucket flipped to private after the release).
- [x] Attachment uploads: the type is read from the file's bytes (photos and PDF only), the
  client's content type is ignored; deleting an attachment deletes its file.
- [x] Quote delete guards: refused while an installation is scheduled or once the order is in the
  ERP. A draft, or a confirmed quote nothing depends on, can still be deleted until §13 brings
  *Cancelled*.
- [ ] Still open from the two above: F-Gas delete (goes with the void entry of §17); deleting a
  quote leaves its PDF and attachment files in storage.
- [ ] Rate limiter, HSTS, forwarded headers, mock-ERP production guard. Needs the deployment's
  proxy topology: a limiter keyed on the wrong client address throttles everyone at once.
- [ ] Audit entries + interceptor + admin viewer; client data export and anonymise.
- [ ] Quote number sequence per store; concurrency test in the Postgres suite.
- [ ] Dependency and secret scanning in CI (`dotnet list package --vulnerable`, `npm audit`,
  secret scan), failing on high severity.

## 12. Administration — users, catalogue and reference data in-app

Goal: nothing a business changes in normal operation needs SQL or a release.

**Today** — the API has no write endpoint for products, price lists, discount tiers, service
items, extra types, incentive schemes, installers, operators, client sources, the lookup tables or
users; `LookupsController` is read-only and `seed` is the only writer. RLS already limits these
tables to Admin writes, so the screens add no new policy shape.

**Screens** (all `AdminOnly`, all on the §8 data table)
- **Members** (§6, §11): the tenant's members — invite, role, stores, linked installer, disable;
  for a platform administrator, tenants and their stores.
- **Catalogue**: products (every field of `Product`, photo, active flag), price lists with their
  discount ladder, service items (engine-read `code` rows are protected from deletion and
  re-coding), extra types.
- **Incentive schemes** (from §4): a form per `kind` showing only that kind's parameters, with a
  worked example recomputed live by the same code as the wizard preview.
- **People**: installers and crews (drag installers between crews), operators.
- **Lookups**: labels, colours, order and active flag of statuses, VAT options, day slots,
  departments, client sources. Codes are immutable; a code the engine branches on cannot be
  deactivated.
- **Price-list import**: Excel / CSV upload → dry run showing added / changed / deactivated rows
  and the price delta → apply in one transaction; export in the same layout.

**Rules that come with it**
- **Deactivate, never delete** anything a quote references (the foreign keys already `RESTRICT`).
- **Price history**: `product_prices` (product, valid from, price columns) so a catalogue change is
  dated; a new price list can be loaded ahead of its start date.
- **Reprice policy** (§22): saving a draft re-reads service-item and extra prices from the
  catalogue (`QuoteService.ApplyRequestAsync`), so an old draft silently changes price after a
  catalogue edit. Keep each line's snapshot until the user presses *Reprice*, and show "prices as
  of {date}" on the quote.
- Cached lookups: the SPA re-fetches a lookup when an admin saves it (a SignalR "lookups changed"
  message is enough).

**Steps**
- [ ] Tenants, stores and members screens (with §6 and §11).
- [ ] Generic lookup editor + installers / crews / operators.
- [ ] Products, price lists and tiers, service items, extra types; price history.
- [ ] Incentive schemes editor.
- [ ] Price-list import / export; reprice policy.

## 13. Quote lifecycle — from enquiry to money

Goal: the quote list tells the truth about the pipeline, and the steps around a quote (sending it,
chasing it, getting a yes, collecting the deposit) happen in the product.

**Today** — four statuses (`Draft`, `SurveyDone`, `Recalled`, `Confirmed`) and any transition is
accepted; a quote that will never close stays open forever, so the dashboard's pipeline and "oldest
open" only grow. A recall stores a note but no date to call back. Versions and revisions are
inferred from "same client, same outdoor unit" (`QuoteNumberingService`), with no *Duplicate* or
*New revision* action. `ManualDiscount` is unbounded and no cost is stored, so margin is unknown.
Nothing records a payment.

**Design**
- **State machine** (settles a §22 question): `Draft → SurveyDone → Sent → Confirmed`, with
  `Recalled` as a repeatable event rather than a state, and the exits `Lost`, `Expired`,
  `Cancelled`. Allowed transitions are a `quote_status_transitions` table (from, to, required role)
  enforced in `QuoteService`; statuses stay lookup rows. `Lost` requires a reason from a
  `quote_lost_reasons` lookup.
- **Validity**: `valid_until` (default from a setting, printed on the PDF); a daily job moves
  overdue `Sent` quotes to `Expired`; reopening is a new revision at current prices.
- **Follow-ups**: `quote_follow_ups` (due date, note, owner, done at). A recall creates one; "my
  follow-ups today" on the dashboard; a daily digest by e-mail (§18).
- **Explicit versioning**: *Duplicate*, *New revision* (same number / version) and *New version*
  (an alternative for the same client) as actions; a side-by-side comparison of two revisions; the
  PDF can print up to three versions as options on one page.
- **Send and accept**: *Send to customer* e-mails the PDF (§18) with a link to a tokenised public
  page: the quote, *Accept* (typed name + a code sent by e-mail or SMS) or *Ask a question*.
  Acceptance stores who / when / IP / document hash and confirms the quote through the same
  `ConfirmAsync` path (ERP outbox included). The token is single-purpose, expiring and
  rate-limited.
- **Survey as an appointment**: a site survey is scheduled on the calendar (§14) with a surveyor
  (answers "a surveyor distinct from the agent" in §22); completing it sets `SurveyDone`.
- **Payments**: `quote_payment_terms` (deposit % / balance, as data) → `quote_payments` (date,
  amount, method lookup, reference, recorded by); outstanding balance on the quote and the client;
  a receipt PDF. This is collection tracking, not invoicing (§22).
- **Margin and discount control**: `cost` on products and service items; margin per quote visible
  to Admin; a discount above a per-role threshold needs an Admin approval (`quote_approvals`)
  before the quote can be sent. Pricing changes follow the rule: Application tests plus
  `quote-price-estimate.ts` kept in step.
- **Free-text lines**: an ad-hoc priced line and a memo printed on the PDF (two more §22 fields),
  flagged in the ERP order as off-catalogue.

**Steps**
- [ ] Transition table + enforcement + `Sent` / `Lost` / `Expired` / `Cancelled`, lost reasons,
  `valid_until` and the expiry job; delete guard from §11.
- [ ] Follow-ups + dashboard tile.
- [ ] Duplicate / new revision / new version actions, comparison view.
- [ ] Send by e-mail and the acceptance page (after §18).
- [ ] Payment terms, payments, receipt PDF.
- [ ] Costs, margin, discount approval.
- [ ] Ad-hoc line and memos.

## 14. One field calendar — installations, repairs, surveys

Goal: one place answers "who is where, when" and refuses a plan that cannot work.

**Today** — the calendar shows installations only. Repair visits are `repair_schedule_entries`: one
installer, a date, no slot, created by `POST /api/repairs/{id}/schedule` with no endpoint to change
or remove one, and invisible on the calendar. Nothing checks availability: the same crew can be
booked twice in a slot (`InstallationService.ApplyScheduleAsync`). A quote can have exactly one
installation, so a two-day job cannot be planned. An installation has no state — nothing says it
happened.

**Design**
- **`appointments`** replaces the two shapes: type (`Installation` / `Repair` / `Revision` /
  `Survey` / `Block`), parent (quote or repair), start day + slot, **duration in half-days**, crew
  or individual installers, status (`Planned` → `EnRoute` → `InProgress` → `Done` / `NotDone` with
  a reason), notes, concurrency token. Migration moves `scheduled_installations` and
  `repair_schedule_entries` across with a working `Down`; a Postgres test covers it.
- **Several appointments per job**; the first estimate of duration comes from the quote (units,
  ducted outlets, extras) through coefficients held as data.
- **Conflicts**: overlapping bookings of an installer, leave (§10), and public holidays (a
  calendar table per deployment) are a warning the planner must confirm, enforced server-side so
  the SignalR drag path cannot skip it.
- **Views**: the existing crew week grid and month view gain a type filter and colour, a day
  view, a per-installer view, and an "unscheduled" side list (confirmed quotes with no
  appointment, open repairs with none) to drag from.
- **Zones**: group and sort the unscheduled list by city / postcode; an optional map and
  travel-time hint through a geocoding port (`None` by default, provider by configuration).
- **Feeds**: a private ICS URL per installer; customer confirmation and day-before reminder (§18).
- The hub is renamed from installations to calendar; its contract stays versioned for old tabs.

**Steps**
- [ ] `appointments` + migration + service with conflict rules + tests; repair visits editable.
- [ ] Calendar on the new model: types, multi-slot blocks, unscheduled list, filters.
- [ ] Appointment status and completion; dashboard "today" tile.
- [ ] Holidays and leave in the conflict check; duration estimate.
- [ ] ICS feeds; zones; optional geocoding port.

## 15. Technician mobile app (PWA)

Goal: the crew works from a phone — no printed work order to bring back and retype.

**Today** — field staff have no place in the product: the roles are Admin / Sales / Warehouse,
`AppUser.InstallerId` exists but nothing reads it, and the installation PDF prints empty signature
boxes. Photos attach to quotes only.

**Design**
- **Role `Installer`**: sees only appointments assigned to the linked installer (API filter and
  RLS predicate), with the client's contact and address for those jobs and nothing else.
- **My day**: today's and tomorrow's jobs, tap to call or navigate, the work-order content, the
  floor-plan sketch read-only.
- **On the job**: start / stop (time on site), a checklist per job type (commissioning,
  maintenance, repair — templates are data), photos before / after, materials used (issues stock
  in §19), unit serials by camera barcode scan (creates the installed units of §16), an F-Gas entry
  (§17), notes, *could not complete* with a reason.
- **Sign-off**: customer signature on screen (the sketch component's canvas, single stroke set),
  rendered into a completion report PDF (a new `PdfDocumentType`, editable in the template editor)
  and e-mailed to the customer (§18).
- **Offline-tolerant**: service worker, the day's jobs cached, mutations queued with an
  idempotency key and replayed; photos uploaded in the background. Conflicts resolve "server
  wins, show me" — a technician never plans.
- Same Angular app, a `features/field/` area with its own shell, installable; no app store.

**Steps**
- [ ] `Installer` role, policies, RLS predicates, authorization matrix entries.
- [ ] My day + job detail (online).
- [ ] Checklists, photos on appointments, time on site.
- [ ] Signature + completion report PDF + e-mail.
- [ ] Offline queue and background upload; Playwright run on a phone viewport.

## 16. Installed equipment, warranty and maintenance contracts

Goal: the product remembers what was installed where, and turns that into planned, recurring work.

**Today** — after installation the trail ends. A repair names its unit as free text
(`Repair.Brand`) with an optional quote link; there is no serial number, warranty date or service
history. A client has one address plus a per-quote free-text installation address. Revisions exist
as a repair type but each is created by hand.

**Design**
- **Sites**: `client_sites` (address, city, postcode, access notes, contact on site). Quotes,
  repairs and appointments reference a site; the free-text installation address migrates into it.
- **`installed_units`**: site, product (or free text for units installed by others), serial,
  installed on (from the completed appointment, §14), refrigerant and charge, manufacturer warranty
  end, extended-warranty end (from the quote's warranty scheme), status (active / replaced /
  removed), a QR label linking to the unit page.
- **Client page**: one view of a customer — sites, units, quotes, appointments, repairs, payments,
  F-Gas records, documents, messages — replacing the edit dialog as the place to land.
- **Repairs reference a unit**; the form shows warranty state and the unit's history. A warranty
  claim towards the manufacturer is tracked on the repair (reference, sent, answered) — the
  `AssistanceRequested` / `AssistanceContacted` statuses already model the conversation.
- **Maintenance plans**: `maintenance_plans` (name, visits per year, price, what is included, as
  data) and `maintenance_contracts` (client, units covered, start, renewal date, price, status). A
  daily job creates the next `Revision` repair a configurable number of weeks before it is due and
  drops it in the unscheduled list (§14); renewal reminders go to the customer (§18).
- **Campaigns**: "units installed more than N months ago with no visit since" as a saved filter
  that produces a call list or a batch of offers — the pre-season service drive.

**Steps**
- [ ] Sites; address migration from clients and quotes.
- [ ] Installed units, created at job completion; QR label PDF; unit page.
- [ ] Client page.
- [ ] Repairs linked to units; warranty state and claim tracking.
- [ ] Plans, contracts, the generator job, renewal reminders.
- [ ] Campaign filters.

## 17. F-Gas compliance register

Goal: the refrigerant records satisfy an inspection and the product tells the company when a check
is due.

**Today** — `FGasRecord` holds a gas name as free text, kilograms, a refill flag and a surcharge
flag, attached to a quote or a repair. There is no technician, certificate, unit, or CO₂
equivalent; the nav entry "F-Gas Records" has no page behind it; records can be deleted (§11).

**Design**
- **`refrigerants`** lookup (code, label, GWP, flammability class, active) replacing the free
  text; CO₂-equivalent = kilograms × GWP, stored with the record so a later GWP revision does not
  rewrite history.
- **Record types**: initial charge, top-up, recovery, leak check (with result), repair of a leak,
  decommissioning; each against an **installed unit** (§16), with the technician, their personal
  certificate and the company certificate (number + expiry on installers and on the company;
  expiry reminders).
- **Append-only**: a wrong record is voided by a correcting entry; RLS drops update and delete.
- **Leak-check schedule**: the bands by tonnes of CO₂-equivalent and their intervals are lookup
  rows (the EU regulation sets them, with longer intervals where a leak-detection system is
  fitted); a job computes each unit's next due date and creates the visit like a maintenance
  revision.
- **Cylinders**: `refrigerant_cylinders` (gas, serial, purchased kg) and a movement per record, so
  purchased − charged + recovered reconciles per gas and period.
- **Register page**: filter by period, gas, technician, client, unit; totals in kg and CO₂e;
  export (CSV / PDF) and a per-country export adapter for the national register, behind a port
  like the ERP connector — the format is a deployment setting, not code in the domain.
- **Documents**: an F-Gas report per intervention for the customer (template type in the editor).

**Steps**
- [ ] Refrigerants lookup, record types, technician and certificates; migration of existing rows.
- [ ] Register page and exports; the nav entry gets its route.
- [ ] Unit link (after §16), leak-check schedule and due job.
- [ ] Cylinder ledger and reconciliation.
- [ ] National-register export adapter once the market is known (§22).

## 18. Notifications and document archive

Goal: the product talks — to staff and to customers — and keeps what it issued.

**Today** — nothing is sent to anyone. Generated PDFs overwrite the previous file at the same path
(`x-upsert`), so the document a customer was given cannot be shown again once the quote changes.

**Design**
- **Outbox**, same rule as the ERP (ARCHITECTURE: external calls go through ports and outboxes):
  `notification_outbox` written in the business transaction, sent by a worker with back-off.
- **Port `INotificationChannel`** per channel, chosen by configuration: e-mail (SMTP or a
  transactional API), SMS, a messaging-app business API; `None` by default. Credentials are
  deployment secrets.
- **Templates as data**: per event and language, subject + body with placeholders, edited by an
  admin with a preview; sender name and reply-to from branding.
- **Events — customer**: quote sent, appointment confirmed, reminder the day before, job completed
  with the report, payment receipt, maintenance due, contract renewal, leak check due.
  **Staff**: follow-ups due, urgent repair opened, ERP export failed, certificate expiring, low
  stock (§19).
- **Consent and log**: per client and channel opt-in flags with the date and source; every message
  on the client page (§16) with its delivery status; an unsubscribe link on anything that is not
  transactional.
- **In-app notification centre**: a bell in the toolbar fed by the existing SignalR connection and
  a `user_notifications` table.
- **Document archive**: `documents` (owner entity, type, version, storage path, hash, issued at,
  issued by, sent to). Each *issued* PDF is immutable; regenerating creates a version. Attachments
  on repairs, appointments and units use the same table and the upload rules of §11.

**Steps**
- [ ] Outbox, worker, e-mail channel, template editor, two events end to end (follow-up digest,
  appointment reminder).
- [ ] Consent flags, message log, unsubscribe.
- [ ] Notification centre.
- [ ] Document archive with versions; attachments everywhere.
- [ ] SMS / messaging channel once a provider is chosen (§22).

## 19. Warehouse and purchasing

Goal: know what is in stock, reserve it for confirmed jobs and reorder before it runs out. This is
the largest remaining module; it gives the `Warehouse` role, which can write nothing today, its
work.

**Today** — `products.stock` is an integer that the seeders set and nothing else writes; the
catalogue carries `ean`, `location` and `stock_notes`; service items (consumables) have no stock at
all.

**Design**
- **Ledger, not a counter**: `stock_movements` is append-only (item, location, quantity ±, type:
  receipt / issue to job / return / transfer / adjustment / stocktake, reference, who, when).
  On-hand is the sum, kept in `stock_levels` by the same transaction; `products.stock` becomes a
  read-only projection and is then dropped.
- **Items and locations**: units and consumables share one `stock_items` identity; locations are
  the warehouse, its bins, and each van (a crew's van is a location, so "what is on the van" is a
  query). Units are tracked by serial where the brand provides one.
- **Reservation**: confirming a quote reserves its units and listed materials (available = on
  hand − reserved); the quote wizard shows availability and the expected date when short.
- **Picking**: a picking list per appointment (PDF with barcodes, a `PdfDocumentType`), picked by
  scanning — issue movements against the job; unused material returns on completion (§15).
- **Purchasing**: suppliers, supplier item codes and costs, purchase orders (`Draft → Sent →
  PartiallyReceived → Received / Cancelled`), goods receipt by scanning against the order, reorder
  point and quantity per item with a "to order" list that drafts orders per supplier.
- **Stocktake**: count sheets per location, differences posted as adjustments with a reason.
- **Valuation**: moving average cost per item; feeds the margin of §13.
- **Barcodes**: internal EAN generation for items without one; label printing.
- Policies: a `WarehouseOrAdmin` policy and matching RLS predicates; every table ships with both.

**Steps**
- [ ] Items, locations, movement ledger, levels, the Warehouse policy; migration from
  `products.stock`.
- [ ] Receipts, issues, transfers, adjustments in `features/warehouse/`; scanning.
- [ ] Reservation at confirmation and availability in the wizard.
- [ ] Picking lists and van stock.
- [ ] Suppliers, purchase orders, goods receipt, reorder list.
- [ ] Stocktake, valuation, stock reports.

## 20. Platform and quality

Not features; they keep the features above cheap to build and safe to run.

- [ ] **One background-job runner**: the ERP worker is a hand-rolled `BackgroundService`. §13–§19
  add about eight more jobs (expiry, follow-up digest, reminders, maintenance and leak-check
  generators, notification sender, reorder). A small scheduler over one `jobs` table, claiming
  with `FOR UPDATE SKIP LOCKED`, last run and outcome visible to admins, safe on several
  instances.
- [ ] **Generated API client**: the SPA's `core/models/*.ts` are written by hand next to the C#
  DTOs. Generate the TypeScript types from the OpenAPI document in CI and fail on drift.
- [ ] **End-to-end smoke suite** (Playwright, already in §8 and §9): sign in, create a quote
  through the wizard, generate the PDF, confirm, schedule by drag, complete on a phone viewport.
- [ ] **Migrations on deploy**: applied by hand today (§21). A migration bundle run as a
  pre-deploy step, with the drift check already in CI, plus a rule for changes that need two
  releases (add, backfill, then drop).
- [ ] **Module switches**: `enabled_modules` per deployment (later per tenant, §6) hiding
  navigation and refusing the endpoints of modules a company does not use — warehouse, F-Gas, ERP,
  field app.
- [ ] **Telemetry**: OpenTelemetry traces and metrics (request, EF, outbox depth and age, job
  outcomes) to any OTLP endpoint; alerts on outbox age and failed jobs.
- [ ] **Performance budget**: `GET /api/lookups/products` returns the whole active catalogue with
  its tiers to every wizard; page and search it server-side past a few hundred rows. A seeded
  load test (the demo dataset × 10) with `EXPLAIN` checked for the list queries of §8.
- [ ] **Backups**: point-in-time recovery or scheduled dumps, storage included, and a restore
  drill written down.
- [ ] **Legacy import tool**: a repeatable importer (clients, catalogue, open quotes, repairs) from
  CSV exports of a previous system, with a dry run — the first thing a new deployment asks for.
- [ ] **More languages**: the Transloco and PDF-caption mechanism takes a third language as data;
  a CI check that every key exists in every language file.
- [ ] `prettier --check` in CI once the existing files are tidied (from §21).

## 21. Operations checklist for a deployment

Not code — settings each deployment should have. See the README's *Deployment*.

- [ ] **Apply the `MultiTenancy` migration together with the release that contains it** — the new
  code does not run against the old schema, nor the old code against the new. Postgres 15 or later.
  After it: existing users keep their role as members of tenant 1; existing Admins are also
  platform administrators.
- [ ] A fresh deployment: set `Bootstrap__AdminEmail` to the first administrator's e-mail before
  they sign in; it does nothing once a platform administrator exists.
- [ ] Supabase Auth **sign-ups disabled** (Dashboard → Auth → Sign In / Providers). A stranger who
  signs up no longer gets any access, but there is no reason to let them create accounts — and the
  bootstrap e-mail must not be registrable by someone else.
- [ ] Supabase **Data API** off, or `public` removed from its exposed schemas — the SPA uses
  Supabase for Auth and Storage only.
- [ ] Storage bucket **private** once the signed-URL release of §11 is deployed (public until
  then, or existing links break).
- [ ] Supabase Auth **leaked password protection** on (Dashboard → Auth → Password security).
- [ ] `MockErp__Enabled` unset in production.
- [ ] Render health-check path `/health/live`.
- [ ] Optional: Sentry projects, `Sentry__Dsn` (API) and `HVAC_SENTRY_DSN` (SPA).
- [ ] Before running more than one API instance: Redis and `Redis__ConnectionString`, and the
  export-worker lock of §5.
- [ ] Persist ASP.NET data-protection keys (today they live in the container; only matters for
  anything that relies on them across restarts).
- [ ] Migrations are applied by hand (`dotnet ef database update` or the generated script); CI's
  deploy summary flags pending ones.
- [ ] Database backups and a tested restore (§20).
- [ ] Tidy the frontend files that fail `prettier --check`, then add the check to CI.

## 22. Open product questions

Each needs a business decision, not just code.

- ~~Multi-tenancy: yes or no~~ — **decided: yes**, built in §6. What it left open:
  - **How the login page finds its tenant** for per-tenant branding: by host name (a domain or
    sub-domain per tenant), or one shared look for every tenant?
  - **Shared lookups**: statuses, VAT options, day slots and departments are one set for every
    tenant. Is that enough, or does a tenant need its own labels, colours or VAT set (another
    country)?
  - **Support access**: a platform administrator cannot see a tenant's data without a membership.
    Keep it that way, or add an audited "act as" for support?
  - **Clients across stores**: clients belong to the tenant and every store sees them. Confirm, or
    restrict a store to the clients it created.
- **Who issues invoices** — the main ERP, or this product? It sets the scope of payments (§13) and
  whether an e-invoicing hand-off is needed (§10).
- **Do installers sign in?** §15 assumes a login per installer; the alternative is one shared
  device login per crew, which weakens the audit trail and the F-Gas technician attribution.
- **What counts as acceptance** (§13): a click with an e-mailed code, or a qualified electronic
  signature through a provider?
- **Message channels and provider** (§18): e-mail only, or SMS / a messaging app too, and on whose
  account.
- **Which market's F-Gas register** (§17) and which national export, if any.
- **Reprice policy for drafts** (§12): keep the prices a draft was quoted at, or follow the
  catalogue.
- **Fields with no home yet**: a surveyor distinct from the agent (proposed: the survey
  appointment of §13); a free-text quote memo and an "additional option" memo; an ad-hoc one-off
  priced line (today `ManualDiscount` + `Rounding` are the escape hatches for an off-catalogue
  amount) — all three proposed in §13.
- **Client code auto-sequence**: codes are entered by hand, guarded only by a unique index (409).
  Generate the next code per series, or keep manual entry?
- **Status transition rules are unenforced**: any quote / repair status change is allowed (the
  history is recorded, the legality is not). §13 proposes the quote machine; the repair machine
  needs the same decision — confirm the ten states and their legal moves.
- **Delete permissions**: client deletes are Admin-only and refused while the client has quotes or
  repairs; repair and quote deletes are Sales / Admin. §11 proposes cancelling instead of deleting
  anything confirmed. Confirm per deployment.
- **Placeholder prices**: ducted-accessory `service_items` are seeded with placeholder prices and the
  WEEE take-back amount is a default; a deployment sets real figures.
- **ERP target** (§5) and the **definition of "purchase"** for statistics (§7).
