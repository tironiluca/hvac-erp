# Working on HVAC ERP

Company-agnostic ERP for HVAC installers: .NET 10 API (Clean Architecture) + Angular 21 SPA on
Supabase (Postgres, Auth, Storage). This repository is the single place where the product is
developed; `main` is the default branch and deploys automatically.

## Read first

- [`docs/ROADMAP.md`](docs/ROADMAP.md) — status, plans and open questions. Resume from the first
  unchecked box; tick boxes and add notes there as work lands.
- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — the rules every change follows (dates, lookup
  tables, RLS on every table, role policies, migrations, no customer identity…).
- [`README.md`](README.md) — what each feature does, configuration, deployment, tests.

## Commands

```bash
# Backend (from backend/)
dotnet build HvacErp.sln
dotnet test HvacErp.sln
# Optional integration tests (CI runs them; skipped otherwise):
#   POSTGRES_TEST_CONNECTION="Host=localhost;Username=postgres;Password=postgres"
#   REDIS_TEST_CONNECTION="localhost:6379"
dotnet tool restore
dotnet tool run dotnet-ef migrations add <Name> --project src/HvacErp.Infrastructure --startup-project src/HvacErp.Api
dotnet tool run dotnet-ef migrations has-pending-model-changes --project src/HvacErp.Infrastructure --startup-project src/HvacErp.Api
# Run the API (from backend/src/HvacErp.Api; secrets via dotnet user-secrets, see README)
dotnet run                 # http://localhost:5299, Swagger at /swagger
dotnet run -- seed         # lookup data
dotnet run -- seed-demo    # large demo dataset

# Frontend (from frontend/)
npm install
npm start                  # http://localhost:4200
npx ng test --watch=false  # Vitest
npm run build              # production build (needs the HVAC_* variables)
```

## Rules of thumb

- Never commit a customer's name, address, host, project id, key or data. Deployment specifics are
  environment variables and database rows.
- A new table: decide who owns it — a store (`IStoreOwned`), a tenant (`ITenantOwned`) or everyone —
  then migration + tenant reference, composite keys and RLS policies (`TenancySql`) + coverage test
  entries. Never filter or stamp `TenantId` / `StoreId` by hand: `AppDbContext` does both. See
  *Tenancy* in `docs/ARCHITECTURE.md`.
- A new mutating endpoint: a role policy, XML docs, an API test. Reach line tables through their
  parent so the store filter applies.
- A stored file: keep its path on the row, say `HasX` in the DTO, and issue a signed link from an
  endpoint that loads the owning record first. No document URLs in DTOs, logs or the database.
- A pricing change: Application tests plus the frontend preview (`quote-price-estimate.ts`) kept in
  step with `QuotePricingService`.
- Update the README and `docs/ROADMAP.md` in the same change when behaviour or status changes.
- Migrations are not applied by CI; note in the commit when a deployment needs one.
