# MinimalApiCrankDemo: Inefficient Baseline

A .NET 10 Minimal API lab for measuring intentionally inefficient SQL Server,
EF Core, and ASP.NET Core Identity access with Crank and Bombardier. SQL Server
runs under Podman; the API listens on `http://localhost:8640`.

The self-contained inefficient baseline is under `01-inefficient`. It
intentionally contains no optimized implementation or comparison solution.
Run the remaining commands from that directory:

```powershell
Set-Location .\01-inefficient
```

## Architecture

- `MinimalApiCrankDemo.Api` owns HTTP endpoint mapping, authentication,
  middleware, and dependency injection. Startup only starts the API.
- `MinimalApiCrankDemo.Application` owns DTOs, repository abstractions,
  application-service interfaces, and application services. It has no EF Core
  dependency.
- `MinimalApiCrankDemo.Database` owns Identity/database entities, the DbContext,
  entity configurations, the design-time context factory, and schema-only EF
  Core migrations. It contains no seed data.
- `MinimalApiCrankDemo.Infrastructure` implements application repositories with
  Identity and intentionally chatty EF Core queries.
- Integration and benchmark SQL data lives under `seed/`, outside every
  application project.

## Prerequisites and Podman

- .NET 10 SDK
- Podman Desktop or Podman CLI with Compose support
- PowerShell 7 (`pwsh`)

Start the Podman machine:

```powershell
podman machine start
```

Copy `.env.example` to the ignored `.env` and replace the SQL Server SA
password. Integration setup creates a random local `.env` if it is missing.
No production secret or plaintext application password is stored in
source-controlled configuration.

## Integration tests

Press **Run Tests** in the IDE, or run:

```powershell
dotnet test .\MinimalApiCrankDemo.slnx
```

The integration fixture invokes `scripts/Prepare-IntegrationDatabase.ps1`,
which performs these operations in order:

1. starts the Podman SQL Server container;
2. waits until SQL Server is reachable;
3. recreates `CrankDemo`;
4. runs `scripts/Invoke-Migrations.ps1`;
5. verifies migrations inserted no Identity rows; and
6. runs `scripts/Seed-IntegrationDatabase.ps1` twice.

The second seed pass and integration row-count assertions verify idempotence.
The fixture then starts the API test host and exercises authentication and the
products endpoint. It leaves SQL Server running for fast repeat runs.

The test and benchmark Identity account is:

- Email/user name: `demo@example.com`
- Password: `DemoPassword123!`

The SQL contains only an ASP.NET Core Identity-compatible password hash.

## Migrations and SQL seeds

Migrations and seeding are separate operations. The API never runs either.

Apply the Database project's schema-only EF Core migrations:

```powershell
.\scripts\Invoke-Migrations.ps1
```

Seed an already migrated integration database:

```powershell
.\scripts\Seed-IntegrationDatabase.ps1
```

Seed an already migrated benchmark database:

```powershell
.\scripts\Seed-BenchmarkDatabase.ps1
```

Both seed commands use container `sqlcmd` and the ignored `.env`. Their
idempotent SQL files are:

- `seed/integration/001-seed-data.sql`
- `seed/benchmark/001-seed-data.sql`

## Start and call the API

Prepare the database first, then run:

```powershell
.\scripts\Start-Api.ps1
```

Login uses ASP.NET Core Identity's built-in proprietary bearer-token handler;
the project does not generate custom JWTs.

```powershell
$login = Invoke-RestMethod `
  -Uri 'http://localhost:8640/api/auth/login' `
  -Method Post `
  -ContentType 'application/json' `
  -Body '{"email":"demo@example.com","password":"DemoPassword123!"}'

$headers = @{ Authorization = "Bearer $($login.accessToken)" }
Invoke-RestMethod `
  -Uri 'http://localhost:8640/api/products' `
  -Headers $headers
```

## Run authenticated Crank

Run:

```powershell
.\crank\Run-Crank.ps1
```

The launcher:

1. verifies Podman is running and starts SQL Server;
2. runs Database-project migrations;
3. runs `Seed-BenchmarkDatabase.ps1`;
4. starts the API if it is not already running;
5. logs in through `POST /api/auth/login`;
6. passes the returned bearer token to the `products` scenario; and
7. runs Bombardier against authenticated `GET /api/products`.

The scenario remains in `crank/crank.yml`; the token is never written to disk.

## Intentional optimization targets

The repository implementations are inefficient by design. Login validates
through Identity, then independently reloads the user, profile, role links,
each role, each role's permissions, preferences, and recent login history.

Products independently reloads the user, preferences, roles, and grants. It
then loads a product index and performs redundant per-product queries for the
product, category, inventory, prices, reviews, and related products.

Comments in `MinimalApiCrankDemo.Infrastructure/Repositories` label these
optimization targets. Possible experiments include composed projections,
joins, eager or batched loading, compiled queries, and caching.

## Reset everything

Remove the SQL Server container and disposable named volume:

```powershell
.\scripts\Reset-Database.ps1
```

The script runs `podman compose down -v`.
