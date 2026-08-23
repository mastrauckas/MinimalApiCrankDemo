# MinimalApiCrankDemo: Inefficient Baseline

A .NET 10 Minimal API lab for measuring intentionally inefficient SQL Server,
EF Core, and ASP.NET Core Identity access with Crank and Bombardier. SQL Server
runs in a container; the API listens on `http://localhost:8640`.

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

## Prerequisites

- .NET 10 SDK
- PowerShell 7 (`pwsh`)
- Either Podman with Compose support or Docker Desktop/Docker Engine

### Install Crank

Crank is required only to run the benchmark. You do not need it to build the
API or run unit and integration tests.

Install and verify the controller and agent:

```powershell
dotnet tool install --global Microsoft.Crank.Controller --version "0.2.0-*"
dotnet tool install --global Microsoft.Crank.Agent --version "0.2.0-*"

crank --help
crank-agent --help
```

The first tool provides the `crank` command. The second provides the local
`crank-agent` process, which executes benchmark jobs.

Manual installation is optional. `crank\Run-Crank.ps1` already installs or
updates both tools before it runs the benchmark.

## Container Runtime and Integration Tests

Choose **either Podman or Docker** for this demo. Do not run both at the same
time: both Compose projects use the same SQL Server port and container name.

Copy `.env.example` to the ignored `.env` and replace the SQL Server SA
password before starting Compose. Integration setup creates a random local
`.env` if it is missing. No production secret or plaintext application
password is stored in source-controlled configuration.

### Podman on Windows

Run these commands from `01-inefficient`:

```powershell
podman --version
podman machine start
podman info
podman compose up -d sqlserver
podman compose ps
podman compose logs sqlserver
dotnet test .\MinimalApiCrankDemo.slnx
```

`podman --version` confirms that the Podman CLI is installed. On Windows,
`podman machine start` starts the Linux virtual machine used by Podman.
`podman info` confirms that the CLI can reach that machine. The Compose
commands start SQL Server, show its status, and display its logs. The final
command runs all tests.

### Docker

Run these commands from `01-inefficient`:

```powershell
docker version
docker compose up -d sqlserver
docker compose ps
docker compose logs sqlserver
dotnet test .\MinimalApiCrankDemo.slnx
```

`docker version` confirms that Docker Desktop or Docker Engine is running.
The Compose commands start SQL Server, show its status, and display its logs.
The final command runs all tests.

The current automated integration fixture invokes the Podman-specific
`scripts/Prepare-IntegrationDatabase.ps1`. The Docker commands above are the
equivalent container workflow, but a Docker-only test run will require runtime
selection support in that script. This README-only change does not alter the
existing test behavior.

You can also press **Run Tests** in the IDE. The integration-test lifecycle is:

1. Start SQL Server if needed.
2. Wait for SQL Server to accept connections.
3. Recreate the disposable `CrankDemo` database.
4. Run `scripts/Invoke-Migrations.ps1`.
5. Verify migrations inserted no Identity or application seed data.
6. Run `scripts/Seed-IntegrationDatabase.ps1` twice to verify idempotency.
7. Start the API test host.
8. Run the integration tests.

The SQL Server container remains running after the tests, which makes repeated
test runs faster.

### Reset the disposable database

Use the command for your chosen runtime.

Podman:

```powershell
podman compose down -v
```

Docker:

```powershell
docker compose down -v
```

`down -v` removes the SQL Server container and its named volume. The next run
therefore starts with a completely clean database.

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
