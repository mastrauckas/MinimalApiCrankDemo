# MinimalApiPerformanceDemo: Inefficient Baseline

A .NET 10 Minimal API lab for measuring intentionally inefficient SQL Server,
EF Core, and ASP.NET Core Identity access. Crank, k6, and Siege configurations
live alongside the same API; SQL Server runs in a container and the API listens
on `http://localhost:8640`.

The self-contained inefficient baseline is under `01-inefficient`. It
intentionally contains no optimized implementation or comparison solution.
Run the remaining commands from that directory:

```powershell
Set-Location .\01-inefficient
```

## Benchmark tools

All tools exercise the same API and database; the application is not copied per
tool. Benchmark assets are grouped by tool beneath `benchmarks/`:

- `benchmarks/crank/` contains the current authenticated products benchmark.
- `benchmarks/k6/` is reserved for the equivalent k6 scenario.
- `benchmarks/siege/` contains the equivalent authenticated Siege scenario.

## Architecture

- `MinimalApiPerformanceDemo.Api` owns HTTP endpoint mapping, authentication,
  middleware, and dependency injection. Startup only starts the API.
- `MinimalApiPerformanceDemo.Application` owns DTOs, repository abstractions,
  application-service interfaces, and application services. It has no EF Core
  dependency.
- `MinimalApiPerformanceDemo.Database` owns Identity/database entities, the DbContext,
  entity configurations, the design-time context factory, and schema-only EF
  Core migrations. It contains no seed data.
- `MinimalApiPerformanceDemo.Infrastructure` implements application repositories with
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

Manual installation is optional. The benchmark entry points install or update
both tools through the lower-level `benchmarks\crank\Run-Crank.ps1` script.

## Container Runtime and Integration Tests

Choose **either Podman or Docker** for a test run. Do not run both. Integration
tests use `127.0.0.1:14333` and manage their own Compose project.

The fixture creates a strong random SQL Server `sa` password for every run. It
keeps the password in memory and passes it to Compose, migrations, SQL seed
commands, and the API test host through process environment variables. It
never writes the password to a file or test output.

### Podman on Windows

Run these commands from `01-inefficient`:

```powershell
podman --version
podman machine start
podman info
dotnet test .\MinimalApiPerformanceDemo.slnx
```

`podman --version` confirms that the Podman CLI is installed. On Windows,
`podman machine start` starts the Linux virtual machine used by Podman.
`podman info` confirms that the CLI can reach that machine. Podman is the
default integration-test runtime.

### Docker

Run these commands from `01-inefficient`:

```powershell
docker version
$env:PERFORMANCE_DEMO_CONTAINER_RUNTIME = 'docker'
dotnet test .\MinimalApiPerformanceDemo.slnx
Remove-Item Env:PERFORMANCE_DEMO_CONTAINER_RUNTIME
```

`docker version` confirms that Docker Desktop or Docker Engine is running.
Set `PERFORMANCE_DEMO_CONTAINER_RUNTIME` to select Docker for that PowerShell
session. Remove it afterward to restore the Podman default.

You can also press **Run Tests** in the IDE. The integration-test lifecycle is:

1. Generate a random `sa` password in memory.
2. Remove the previous test container and named volume.
3. Start a fresh test-only SQL Server container.
4. Authenticate from Windows through `127.0.0.1:14333`.
5. Recreate the disposable `PerformanceDemo` database.
6. Run `scripts/Invoke-LocalMigrations.ps1`.
7. Verify migrations inserted no Identity or application seed data.
8. Run `scripts/Seed-IntegrationDatabase.ps1` twice.
9. Start the API test host and run the integration tests.
10. Remove the test container and named volume, even after a failure.

Each run starts from an empty volume, so integration tests can take longer
than unit tests. The dedicated container and volume names protect the
developer and benchmark database.

### Reset the disposable database

Use the command for your chosen runtime.

Podman:

```powershell
podman compose --file docker-compose.integration-tests.yml down -v
```

Docker:

```powershell
docker compose --file docker-compose.integration-tests.yml down -v
```

The fixture runs this cleanup automatically. Use these commands only to clean
up after an interrupted test process.

The test and benchmark Identity account is:

- Email/user name: `demo@example.com`
- Password: `DemoPassword123!`

The SQL contains only an ASP.NET Core Identity-compatible password hash.

## Migrations and SQL seeds

Migrations and seeding are separate operations. The API never runs either.
For a manual developer or benchmark database, put the SQL Server settings in
the current PowerShell process rather than a file:

```powershell
$saCredential = Get-Credential -UserName sa
$env:MSSQL_SA_PASSWORD = $saCredential.GetNetworkCredential().Password
$env:SQLSERVER_HOST = '127.0.0.1'
$env:SQLSERVER_PORT = '14333'
$env:SQLSERVER_DATABASE = 'PerformanceDemo'
$env:SQLSERVER_USER = 'sa'
podman compose up -d sqlserver
```

Use `docker compose` on the last line if Docker is your chosen runtime.

Apply the Database project's schema-only EF Core migrations for local test or
benchmark tooling:

```powershell
.\scripts\Invoke-LocalMigrations.ps1
```

Seed an already migrated integration database:

```powershell
.\scripts\Seed-IntegrationDatabase.ps1
```

Seed an already migrated benchmark database:

```powershell
.\scripts\Seed-BenchmarkDatabase.ps1
```

The fixture and Crank launcher provide database settings through their process
environment. Both seed commands use container `sqlcmd`. Their idempotent SQL
files are:

- `seed/integration/001-seed-data.sql`
- `seed/benchmark/001-seed-data.sql`

## Production Migrations

The production API must never apply migrations automatically at startup.
Deployments apply a reviewed EF Core migration bundle before starting the new
API version.

Build a versioned bundle from the Database project:

```powershell
.\scripts\Build-MigrationBundle.ps1 -Version '1.0.0'
```

This command creates
`artifacts/migration-bundles/1.0.0/efbundle.exe`. The script runs the
equivalent EF Core command:

```powershell
dotnet tool run dotnet-ef migrations bundle `
  --project .\src\MinimalApiPerformanceDemo.Database `
  --startup-project .\src\MinimalApiPerformanceDemo.Database `
  --output .\artifacts\migration-bundles\1.0.0\efbundle.exe `
  --force
```

Use this deployment order:

1. Build the versioned migration bundle.
2. Back up the production database.
3. Obtain the connection string from an Azure DevOps secret variable or
   Azure Key Vault.
4. Run `Invoke-ProductionMigrations.ps1`.
5. Deploy or restart the API.
6. Run the API health checks.

For example, map the secret to a masked pipeline environment variable, then
run:

```powershell
.\scripts\Invoke-ProductionMigrations.ps1 `
  -ConnectionString $env:PRODUCTION_SQL_CONNECTION `
  -BundlePath '.\artifacts\migration-bundles\1.0.0\efbundle.exe'
```

The wrapper contains no connection string or environment-specific value. The
migration workflow applies schema changes only; it never runs production seed
data.

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

For a complete Podman benchmark run:

```powershell
.\scripts\Invoke-BenchmarkSetupAndCrank.ps1
```

The script creates a dedicated benchmark SQL Server container and volume. It
uses database `PerformanceDemoBenchmark` through `127.0.0.1:14334`, runs migrations
and benchmark seeding, starts the API on port 8640, logs in through Identity,
and runs the existing products scenario. It stops only the API afterward and
leaves SQL Server running for inspection.

To remove the benchmark container and volume after the run:

```powershell
.\scripts\Invoke-BenchmarkSetupAndCrank.ps1 -Cleanup
```

To remove an existing benchmark database without running a benchmark:

```powershell
.\scripts\Remove-BenchmarkDatabase.ps1
```

For Docker, add `-ContainerRuntime Docker`.

Select Docker explicitly when needed:

```powershell
.\scripts\Invoke-BenchmarkSetupAndCrank.ps1 `
  -ContainerRuntime Docker
```

After one complete setup run has retained the benchmark container and seeded
database, rerun only the authenticated load test:

```powershell
.\scripts\Invoke-CrankOnly.ps1
```

If the API is not already running, this command privately reads the disposable
SQL password from the existing benchmark container, starts the API, runs Crank,
and stops the API afterward. It does not recreate the container, migrate the
database, or seed data. If the API is already running, it uses that instance.

Both entry points save timestamped JSON results under
`artifacts/benchmarks/crank/` and print the exact result path. The scenario
remains in `benchmarks/crank/crank.yml`; bearer
tokens and SQL Server passwords are never written to the result file or logs.

`benchmarks/crank/Run-Crank.ps1` is now the lower-level scenario runner. It expects a
bearer token and result path, so use the two entry points above for normal
benchmark work.

## Run authenticated Siege

Siege runs in a small local Linux container because it does not support native
Windows execution. The runner builds that image automatically on its first run.
It runs the same products request as Crank: 32 concurrent clients, a 5-second
warmup, then a 15-second measured run.

For a complete Podman benchmark run:

```powershell
.\scripts\Invoke-BenchmarkSetupAndSiege.ps1
```

After the benchmark database has been prepared, rerun only the Siege load test:

```powershell
.\scripts\Invoke-SiegeOnly.ps1
```

For Docker, add `-ContainerRuntime Docker` to either command. Siege prints its
transactions, elapsed time, response time, transaction rate, throughput,
concurrency, and success/failure counts in the terminal. The measured output is
also saved under `artifacts/benchmarks/siege/`.

On Windows, the Podman runner discovers the active WSL virtual-network gateway
to reach the API. This avoids relying on the host alias, which may be unable to
reach a Windows loopback port in rootless Podman.

## Intentional optimization targets

The repository implementations are inefficient by design. Login validates
through Identity, then independently reloads the user, profile, role links,
each role, each role's permissions, preferences, and recent login history.

Products independently reloads the user, preferences, roles, and grants. It
then loads a product index and performs redundant per-product queries for the
product, category, inventory, prices, reviews, and related products.

Comments in `MinimalApiPerformanceDemo.Infrastructure/Repositories` label these
optimization targets. Possible experiments include composed projections,
joins, eager or batched loading, compiled queries, and caching.
