# MinimalApiCrankDemo

A .NET 10 Minimal API lab for measuring intentionally inefficient SQL
Server, EF Core, and ASP.NET Core Identity access with Crank and Bombardier.
SQL Server runs under Podman; the API runs at `http://localhost:8640`.

## Prerequisites and Podman

- .NET 10 SDK
- Podman Desktop (or Podman CLI with Compose support)
- PowerShell 7 (`pwsh`)

Start the Podman machine:

```powershell
podman machine start
```

No infrastructure secret is committed. The integration setup creates an
ignored `.env` containing a random disposable SQL Server SA password when one
does not exist. To choose your own, copy `.env.example` to `.env` and replace
its placeholder with a strong local password.

## Database and integration tests

Press **Run Tests** in the IDE, or run:

```powershell
dotnet test .\MinimalApiCrankDemo.slnx
```

The integration fixture runs `scripts/Prepare-IntegrationTests.ps1` before the
HTTP tests. It starts `sqlserver` with Podman Compose, waits for SQL Server,
and recreates `CrankDemo`. It then invokes two separate operations in order:

1. `scripts/Apply-Migrations.ps1` applies schema-only EF Core migrations.
2. `scripts/Seed-DemoData.ps1` runs the explicit Identity/catalog seed command.

The test setup runs the seed command a second time, and an integration test
asserts that users, roles, products, permissions, and related rows were not
duplicated. The container stays running for fast repeat runs.

The seeded demo Identity account is:

- Email/user name: `demo@example.com`
- Password: `DemoPassword123!`

These are deliberately public demo credentials, not an infrastructure secret.
Identity password hashing occurs at seed time; no password hash is stored in
source.

Database recreation is defined under `seed/`; schema is defined by the EF Core
migrations in `src/MinimalApiCrankDemo.Api/Data/Migrations`.

## Migrate and seed are separate commands

Normal API startup never applies migrations and never seeds data.

To apply pending schema migrations only, run:

```powershell
.\scripts\Apply-Migrations.ps1
```

This wrapper restores the repo-local `dotnet-ef` tool and runs the equivalent
of the project's migration command:

```powershell
dotnet tool run dotnet-ef database update `
  --project .\src\MinimalApiCrankDemo.Api\MinimalApiCrankDemo.Api.csproj `
  --startup-project .\src\MinimalApiCrankDemo.Api\MinimalApiCrankDemo.Api.csproj
```

That command changes schema only. It does not invoke the demo seeder.

After the database is migrated, seed the development/demo rows explicitly:

```powershell
.\scripts\Seed-DemoData.ps1
```

The wrapper loads the ignored `.env` connection settings and invokes:

```powershell
dotnet run --project .\src\MinimalApiCrankDemo.Api `
  -- --seed-demo-data
```

The seed command refuses to run while migrations are pending. It is
idempotent, so repeated runs preserve one demo user, the expected roles and
permissions, and one copy of every catalog and related row.

## Start and call the API

Recreate, migrate, and seed through the tests once, then start the API:

```powershell
.\scripts\Start-Api.ps1
```

Login uses ASP.NET Core Identity's built-in proprietary bearer-token handler.
The project does not create JWTs or implement custom token generation.

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

`POST /api/auth/login` is anonymous. `GET /api/products` requires
`Authorization: Bearer <token>`. Example requests are in
`http-files/demo.http`; paste the returned token into its `accessToken`
variable.

## Run authenticated Crank load

Keep the API running, then execute:

```powershell
.\crank\Run-Crank.ps1
```

The exact flow performed by the script is:

1. install or update the Crank controller and local agent;
2. `POST /api/auth/login` with the seeded Identity credentials;
3. read `accessToken` from Identity's response;
4. pass `bearerToken=<token>` to the `products` Crank scenario; and
5. run Bombardier against `GET http://localhost:8640/api/products` with
   `Authorization: Bearer <token>`.

The scenario is in `crank/crank.yml`. It never writes the token to disk.

## Intentional optimization targets

The endpoint code is inefficient by design. It is a lab, not production data
access guidance.

After Identity validates login, the login handler deliberately reloads the
user, then separately queries profile, user-role links, each role, each role's
permissions, preferences, and recent-login history. It also performs a
separate recent-login insert. Possible experiments include a purpose-built
read projection, fewer round trips, batched queries, or caching stable role and
permission data.

The products handler deliberately reloads the authenticated user, separately
loads preferences and role grants, loads full product rows as an index, then
requeries every product. Each visible product triggers separate category,
inventory, price, review, and related-product queries. Optimization exercises
include a composed projection, joins, eager or explicit batched loading,
compiled queries, and caching. Change one pattern at a time and compare it to
the same Crank baseline.

Comments in `Endpoints/DemoEndpoints.cs` label every intentional inefficiency.

## Reset everything

The reset script removes the SQL Server container and disposable named volume:

```powershell
.\scripts\Reset-Database.ps1
```

It explicitly runs `podman compose down -v`.
