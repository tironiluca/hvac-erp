# HVAC ERP

Management portal for HVAC installers — clients, quotes with a pricing engine, repairs, a real-time
installation calendar, F-Gas records and generated PDFs — on a **C# (.NET) API + Angular** stack,
backed by **Supabase**.

The product carries no customer identity: a deployment's company name, colours, logo, PDF footer,
storage bucket and hosts are configuration and data (see [Configuration](#configuration)), never code.

## Architecture

```
Angular SPA (Vercel)  ──HTTPS──►  .NET API (Render)  ──────►  Supabase Postgres
        │                                                        │
        └───────────────────HTTPS (Auth + Storage)───────────────┘
```

- **Frontend** (`frontend/`) — Angular 21, standalone components, Angular Material, [Transloco](https://jsverse.gitbook.io/transloco) for i18n (`it` default, `en` available). Talks to the API for business data and directly to Supabase for authentication (sign-in/session).
- **Backend** (`backend/`) — .NET 10, Clean Architecture across 4 projects:
  - `HvacErp.Domain` — entities, enums, no dependencies.
  - `HvacErp.Application` — DTOs, service interfaces, business logic (pricing, quote numbering, workflows).
  - `HvacErp.Infrastructure` — EF Core (Npgsql, snake_case naming) against Postgres, a Supabase Storage client for file uploads, QuestPDF document generators.
  - `HvacErp.Api` — controllers, DI composition root, JWT bearer auth, role policies, SignalR hub, health checks and request logging.
- **Supabase** — Postgres database, Auth, and Storage (one bucket, `documents` by default, for quote/repair PDFs and photo attachments).

**Auth** is delegated entirely to Supabase Auth — there's no custom password/JWT issuing code in the API. The Angular app authenticates directly against Supabase and attaches the resulting access token as a Bearer header on API calls; the API validates the Supabase-issued JWT (ES256, verified via Supabase's JWKS discovery endpoint, no shared secret) and upserts an `AppUser` profile row (role + optional installer link) keyed by the Supabase user id.

**Dates** are always stored and handled in UTC. All timestamp columns are Postgres `timestamp with time zone`, and `AppDbContext.ConfigureConventions` (`AppDbContext.cs`) applies a value converter to every `DateTime`/`DateTime?` property so anything written is normalized to `Kind=Utc` — Npgsql otherwise throws or silently misbehaves on mismatched `DateTimeKind`. Use `DateTime.UtcNow` for server-generated timestamps; only convert to local time at the presentation layer. The one deliberate exception is date-only inputs (e.g. an installation day picked from a datepicker): the frontend serializes those via `toLocalDateTimeIso` (`frontend/src/app/core/utils/date.util.ts`) using the picked calendar date's local Y/M/D rather than `Date.prototype.toISOString()`, because a UTC conversion can shift a date-only value to the previous/next day depending on the browser's offset — the EF Core converter then normalizes that naive string to `Kind=Utc` on save without altering the calendar date.

**Portal branding** is white-label and admin-editable at runtime — there are no compile-time brand constants. A single-row `PortalBranding` record (three Material 3 colour seeds, portal title, and logo / favicon / login-background images) is served by `GET /api/branding` (anonymous, so it applies on first paint and on the login page) and edited by admins at `/settings/branding`. The Angular app maps the seeds onto the full `--mat-sys-*` Material 3 token set at bootstrap; the same title and logo are rendered into the generated PDF headers.

**Incentive schemes** — tax incentives, fees, invoice discounts and per-line options (an energy-efficiency deduction, equipment take-back, a 50% invoice discount, an extended warranty) are rows in `incentive_schemes`, not code. Each row picks one of three built-in calculations and stores its parameters, so a rate, threshold or rounding change is a row edit:

| `kind` | Calculation | Parameters |
|---|---|---|
| `QuoteAmount` | One amount per quote, entered in the wizard (else `default_amount`). | `add_to_price` (charged after VAT, else shown only), `include_in_invoice_discount_basis` |
| `LineAmount` | Per product line, as the salesperson picks on the line: a % of the list unit price or a fixed € amount, both per unit. A newly ticked line starts from the scheme's `percent` when set, else its `default_amount`; the line stores the mode and value it was priced with. | `add_to_price` (charged on the machines side, before VAT) |
| `InvoiceDiscount` | Basis = final price − manual discount + flagged quote amounts; × `rate_below_threshold` below `threshold`, else × `rate_above_threshold`; raised to the next `rounding_step` (a full step even on an exact multiple, as legacy did). The customer pays `percent` %. | `requires_vat`, `requires_confirmation` (accepted per quote via `POST /api/quotes/{id}/incentives/{schemeId}/confirmation`) |

`pdf_label` is printed in the quote PDF's pricing summary (the block title, for an invoice discount) and `pdf_invoice_discount_note` inside the invoice-discount block. Check constraints require each kind's parameters. `quote_incentives` / `quote_product_incentives` keep the amounts each quote was priced with, and the wizard's live preview (`quote-price-estimate.ts`) mirrors the same calculations from `GET /api/lookups/incentive-schemes`. There is no admin screen for the schemes yet: edit the rows in SQL.

**Floor-plan sketches** — the quote, repair and installation forms embed a hand-rolled HTML5-canvas drawing tool (`shared/floor-plan-sketch/`, a `ControlValueAccessor`) replacing the legacy Windows-Ink form. Strokes are stored as resolution-independent **vector JSON** in a `jsonb` column (`FloorPlanSketchJson` on each entity), validated server-side by `FloorPlanSketchValidator`, and rendered into the generated PDFs as SVG via QuestPDF's `.Svg()` element — no image files are stored.

**Real-time installation calendar** — `/installations` is a drag-and-drop scheduling calendar with a **week** view (a crew resource grid: crews as rows, days as columns; the mattina/pomeriggio bands collapse to a single "Giornata" row when that crew is booked all day) and a **month** view; both support drag-to-reschedule. It live-syncs across every connected user over a **SignalR** hub (`/hubs/installations`) — server→client fan-out of appointment changes, plus a presence roster so each viewer sees who else has the page open. A dead hub degrades to REST-only. Drags are optimistic and guarded by a per-row `ConcurrencyToken`; a racing edit surfaces as a 409 and refetches. Crews come from `Installer.Crew` (a `GET /api/lookups/installers` field); `DevDataSeeder` seeds eight of them.

**Authorization** is role-based (`UserRole`: `Admin` / `Sales` / `Warehouse`, held on the `AppUser` row and added to the principal as a role claim). Every endpoint requires a signed-in user. Reads of business data are open to any role. Creating, editing and deleting clients, quotes, repairs, installations and F-Gas records requires `SalesOrAdmin`. Branding, PDF templates and deleting a client require `AdminOnly` (see `Api/Authorization/Policies.cs`). Confirming a quote is checked again in the Application layer. The same matrix is enforced in the database by **Postgres row-level security** (migration `EnableRowLevelSecurity`), so the Supabase Data API cannot bypass it. `anon` gets no access, and `authenticated` users get exactly their role's rights via `app_private.current_app_role()`. The API connects as the table owner and is unaffected.

**Observability** — `GET /health/live` (process up; use it as the Render health-check path) and `GET /health` (database + Storage readiness, 503 when either is down), both anonymous. Each request gets an `X-Correlation-Id` (taken from the SPA's header or minted). It is echoed on the response, stamped on every log line via a logging scope, and returned as `correlationId` in error responses. Outside Development, logs are one JSON object per line. Errors go to Sentry on both the API and the SPA when a DSN is configured; nothing is sent otherwise, and no PII is included.

**Scale-out** — the calendar's SignalR fan-out and presence roster are in-process by default, which is exact for the single Render instance. Setting `Redis:ConnectionString` switches on the SignalR Redis backplane plus a Redis presence store, with heartbeats and crash expiry. Do this before running more than one API instance.

## Repository layout

```
backend/                         .NET 10 solution (HvacErp.sln)
  src/HvacErp.Api
  src/HvacErp.Application
  src/HvacErp.Domain
  src/HvacErp.Infrastructure
  tests/HvacErp.Application.Tests   xUnit: pricing, numbering, workflows, validators, Postgres integration
  tests/HvacErp.Api.Tests           xUnit + WebApplicationFactory: role matrix, health, presence, doc coverage
  dotnet-tools.json              pins dotnet-ef (run `dotnet tool restore`)
frontend/                        Angular 21 app (see frontend/README.md)
.github/workflows/ci.yml         CI: build, tests, migration drift, Docker image, Angular build + tests
frontend/scripts/write-env.mjs   writes the SPA's deployment settings from HVAC_* env vars at build time
```

## Local development

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) 20+ and npm
- A Supabase project (Postgres + Auth + Storage) — see [Configuration](#configuration) below

### Backend

```bash
cd backend/src/HvacErp.Api
dotnet user-secrets set "ConnectionStrings:Postgres" "Host=...;Database=...;Username=...;Password=..."
dotnet user-secrets set "Supabase:Url" "https://<project-ref>.supabase.co"
dotnet user-secrets set "Supabase:ServiceRoleKey" "<service-role-key>"
dotnet run
```

The API listens on `http://localhost:5299` (Swagger UI at `/swagger` in Development). To seed lookup data needed to exercise the UI (installers — eight crews — operators, product types, price lists, sample products), run:

```bash
dotnet run -- seed
```

For a large, interlinked demo dataset to exercise the UI at volume — ~130 clients, an expanded product catalogue, ~220 quotes across every status (with line items, extras, ducted systems and status history), ~160 repairs/revisions across all states, a dense installation calendar (whole-crew assignments plus a packed band of ~400 appointments spanning ±6 weeks around today, to fill the week/month views), and F-Gas records — run:

```bash
dotnet run -- seed-demo
```

Both are idempotent — safe to re-run against a partially-seeded database. `seed-demo` runs the lookup seed first, then skips itself if it has already run (every generated client's code is `DEMO####`; remove them with `DELETE FROM clients WHERE code LIKE 'DEMO%'`, dropping the referencing `scheduled_installations` rows first).

### Frontend

```bash
cd frontend
npm install
ng serve
```

Serves on `http://localhost:4200` using `frontend/src/environments/environment.development.ts`, which points `apiUrl` at the local API and already holds the shared dev Supabase project's URL and publishable (anon) key. That key is public by design, so it is only safe once row-level security is on: apply the `EnableRowLevelSecurity` migration to every database the key can reach.

## Configuration

| Setting | Where | Purpose |
|---|---|---|
| `ConnectionStrings:Postgres` | .NET user-secrets locally / env var in deployment | Supabase Postgres connection string (use the **session pooler** host, e.g. `aws-1-<region>.pooler.supabase.com:5432` — the direct `db.*.supabase.co` host is IPv6-only) |
| `Supabase:Url` | .NET user-secrets / env var | Supabase project URL, e.g. `https://<project-ref>.supabase.co` — also used to derive the JWT issuer/JWKS endpoint for auth |
| `Supabase:ServiceRoleKey` | .NET user-secrets / env var | Service-role key, used server-side only to call Supabase Storage's REST API |
| `Supabase:StorageBucket` | `appsettings.json` | Storage bucket for generated PDFs and attachments (default `documents`; override per deployment with `Supabase__StorageBucket`) |
| `Cors:AllowedOrigins` | `appsettings.json` / env var | Origins allowed to call the API (the deployed frontend URL in production) |
| `Sentry:Dsn` | env var (`Sentry__Dsn`) | Optional. Enables API error reporting to Sentry; empty = off |
| `Redis:ConnectionString` | env var (`Redis__ConnectionString`) | Optional. Enables the SignalR Redis backplane + shared presence; required before scaling the API beyond one instance |
| `HVAC_API_URL`, `HVAC_SUPABASE_URL`, `HVAC_SUPABASE_PUBLISHABLE_KEY`, `HVAC_SENTRY_DSN`, `HVAC_DEFAULT_LANGUAGE` | frontend host env vars (e.g. Vercel project settings) | The SPA's API base URL, Supabase project URL + publishable key, optional Sentry DSN and the UI language for users who have not picked one (`en` default, or `it`). `npm run build` runs `scripts/write-env.mjs`, which compiles them into `environment.ts`; on Vercel a missing one fails the build. Locally, fill in `environment.development.ts` (or run `node scripts/write-env.mjs --dev`) and keep real values out of commits |

.NET config keys use `:` locally (user-secrets/`appsettings.json`) and `__` (double underscore) as environment variables in deployment, e.g. `ConnectionStrings__Postgres`.

No secrets or deployment values are committed: `appsettings.Development.json` / `appsettings.Local.json` are gitignored, and the committed frontend environment files hold placeholders only.

**Company identity is data**: portal title, colour seeds, logo, favicon and login background are edited by an admin at `/settings/branding` (`portal_brandings`), and the PDF header/footer lines in the template editor at `/settings/pdf-templates` (`pdf_templates`). A fresh database starts with neutral "HVAC ERP" defaults.

**Language**: the product defaults are English: seed data, PDF templates and the UI. Italian is built in: set `HVAC_DEFAULT_LANGUAGE=it` for the UI, and pick *PDF language* per template in the editor (it switches the fixed captions such as table headers and totals; *Restore defaults* keeps the template's language). Seeded catalogue rows are ordinary data an admin can rename. The pricing engine finds its service items by `code` (`BASE_SYSTEM_1`…`BASE_SYSTEM_5`, `DUCTED_*`), never by name.

## Deployment

GitHub Actions (`.github/workflows/ci.yml`) runs on every PR and push to `master`:
- backend build and tests, with Postgres and Redis service containers for the integration tests;
- an EF migration-drift check;
- a build of the Render Docker image;
- the Angular production build and Vitest.

Render and Vercel deploy `master` themselves. After a push that adds an EF migration, the CI summary flags it. Apply it to Supabase by hand:

```bash
cd backend && dotnet tool restore
ConnectionStrings__Postgres="<session-pooler connection string>" \
  dotnet tool run dotnet-ef database update --project src/HvacErp.Infrastructure --startup-project src/HvacErp.Api
```

A reference setup (any equivalent host works):

- **Supabase** — Postgres, Auth and Storage. Create a public bucket for documents and set `Supabase__StorageBucket` if it isn't named `documents`.
- **Render** (or any Docker host) — the API from [`backend/src/HvacErp.Api/Dockerfile`](backend/src/HvacErp.Api/Dockerfile): Root Directory `backend`, Dockerfile Path `src/HvacErp.Api/Dockerfile` (the build context needs all 4 projects). It binds to the injected `$PORT` at container start. Env vars: `ConnectionStrings__Postgres`, `Supabase__Url`, `Supabase__ServiceRoleKey`, `Cors__AllowedOrigins__0` (the SPA origin), optionally `Supabase__StorageBucket`, `Sentry__Dsn`, `Redis__ConnectionString`. Health-check path `/health/live`.
- **Vercel** (or any static host) — the SPA per [`frontend/vercel.json`](frontend/vercel.json): Root Directory `frontend`, output `dist/hvac-erp/browser`, SPA rewrite. Set the `HVAC_*` variables above in the project settings; they are compiled in at build time, so changing one needs a redeploy.

## Testing

```bash
# Backend: unit + API tests (in-memory DB, test auth scheme — no external services needed)
dotnet test backend/HvacErp.sln

# Optional integration tests, skipped unless pointed at real servers (CI provides both):
#   POSTGRES_TEST_CONNECTION="Host=localhost;Username=postgres;Password=postgres"  (applies every migration to a scratch DB)
#   REDIS_TEST_CONNECTION="localhost:6379"                                        (Redis presence store)

# Migration drift: fails when the EF model changed without a migration
cd backend && dotnet tool restore && dotnet tool run dotnet-ef migrations has-pending-model-changes \
  --project src/HvacErp.Infrastructure --startup-project src/HvacErp.Api

# Frontend unit tests (Vitest)
cd frontend && npx ng test --watch=false
```

Guard-rail tests worth knowing about:
- `AuthorizationMatrixTests` fails when a mutating endpoint ships without a role policy.
- `XmlDocumentationCoverageTests` fails when a public type or method has no `<summary>`.
- `RowLevelSecurityCoverageTests` fails when a table exists without RLS.
