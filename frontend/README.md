# HVAC ERP — frontend

Angular 21 SPA for the HVAC ERP portal: standalone components, Angular Material 3,
Transloco (`it` default, `en`), zone-based change detection, no SSR. Project-wide architecture,
configuration and deployment live in the [root README](../README.md); this file covers the frontend only.

## Commands

```bash
npm ci                   # install exactly what package-lock.json pins
npx ng serve             # http://localhost:4200, against the local API (environment.development.ts)
npm run build            # writes environment.ts from HVAC_* env vars (if set), then the production build → dist/hvac-erp/browser
npx ng test --watch=false   # Vitest unit tests (CI runs exactly this)
```

The dev build expects the API on `http://localhost:5299` — see the root README to run it.

## Layout

| Folder | What lives there |
|---|---|
| `src/app/core/` | App-wide plumbing: `guards/` (auth, admin), `interceptors/` (Bearer token, correlation id, 403 snackbar), `services/` (one per API area + auth, theme, branding, realtime, error reporting), `models/` (API DTO mirrors), `utils/` (`date.util`, `m3-theme.util`), `shell/` (toolbar + nav rail), `pages/dashboard/`. |
| `src/app/features/<module>/` | Lazy-loaded routed areas, each with its own `<module>.routes.ts`: `auth`, `clients`, `quotes`, `repairs`, `installations`, `settings`. |
| `src/app/shared/` | Reusable components: the F-Gas records section and the floor-plan sketch (`ControlValueAccessor`). |
| `public/i18n/` | `it.json` / `en.json`. Server-driven option sets are labelled by lookup `code` (e.g. `repairs.statuses.InProgress`), so they stay bilingual. |
| `src/environments/` | `apiUrl`, Supabase URL + publishable key, `sentryDsn`. Committed with placeholders; deployments generate `environment.ts` from `HVAC_*` env vars via `scripts/write-env.mjs`. Compiled in at build time. |

## Conventions

- **Dates**: send date-only values with `toLocalDateTimeIso()` (`core/utils/date.util.ts`), never
  `toISOString()`, which shifts the calendar day in positive-UTC offsets.
- **Pricing preview**: `features/quotes/quote-price-estimate.ts` mirrors the server's
  `QuotePricingService`: VAT multipliers from `GET /api/lookups/vat-options` and the incentive-scheme
  calculations (line amounts, charged quote amounts, the invoice discount) from
  `GET /api/lookups/incentive-schemes`. Change both sides together; `quote-price-estimate.spec.ts`
  uses the same cases as the backend tests.
- **Theme**: `styles.scss` defines the Material 3 theme and emits the `--mat-sys-*` colour and
  typography tokens that the shared `.hvac-*` primitives use. `BrandingService` re-derives the colour
  tokens at runtime from the admin-edited brand seeds.
- **Status chips**: use `class="status-chip status-chip--<variant>"` on `mat-chip`. The variants
  set Material's chip tokens, because a plain `background` would be overridden.
- **Roles**: `CurrentUserService.isAdmin()` gates admin-only UI. The API enforces every rule anyway,
  and a refused call surfaces as one "not allowed" snackbar (`forbidden.interceptor.ts`).
- **Errors**: uncaught errors go through `ReportingErrorHandler`. Sentry loads as a lazy chunk only
  when `environment.sentryDsn` is set. Every API call carries an `X-Correlation-Id` that matches the
  server logs.
