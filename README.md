# MinimalApiPerformanceDemo

A .NET 10 performance-comparison lab. `01-inefficient` is the intentionally
chatty EF Core and SQL Server baseline. `02-efficient` will be added later and
will use the same benchmark harness, database schema, seed data, endpoints, and
load settings.

## Layout

```text
01-inefficient/     The current API implementation and its tests.
benchmarks/         Shared Crank, Siege, and future k6 configurations.
seed/               Shared schema reset and idempotent benchmark/test seed SQL.
tools/              File-based C# benchmark, migration, and test setup utility.
```

There are no PowerShell scripts. Run the shared tool with the .NET 10 SDK:

```powershell
dotnet .\tools\RunPerformanceDemo.cs
```

It presents the available tool/implementation combinations:

```text
1. Crank — 01 Inefficient
2. Crank — 02 Efficient (not implemented)
3. Siege — 01 Inefficient
4. Siege — 02 Efficient (not implemented)
5. k6 — 01 Inefficient (not implemented)
6. k6 — 02 Efficient (not implemented)

Benchmark database:
7. Prepare benchmark SQL Server
8. Reset benchmark data
9. Stop and remove benchmark SQL Server
```

The menu prepares the disposable benchmark SQL Server database, applies the
selected implementation's migrations, runs the common seed SQL, starts that
API, logs in with ASP.NET Core Identity, and runs the selected load tool.

## Prerequisites

- .NET 10.0.400 SDK or a later .NET 10 feature band (selected by `global.json`)
- Podman on Windows, including a running Podman machine, or Docker
- For Crank: the tool installs its controller and agent automatically

Start Podman on Windows before running a benchmark or integration test:

```powershell
podman machine start
```

Select Docker instead by setting `PERFORMANCE_DEMO_CONTAINER_RUNTIME=docker` in
the process environment.

## Integration tests

Run the solution from the implementation folder:

```powershell
dotnet test .\01-inefficient\MinimalApiPerformanceDemo.slnx
```

The test fixture invokes the same C# file-based tool with
`prepare-integration` and `cleanup-integration`. It creates an isolated SQL
Server container and volume, generates an in-memory SQL password, recreates the
disposable database, applies schema-only migrations, verifies migrations did
not insert data, and runs the idempotent seed twice.

## Non-interactive commands

Use these commands for repeatable local runs or CI:

```powershell
dotnet .\tools\RunPerformanceDemo.cs prepare-integration
dotnet .\tools\RunPerformanceDemo.cs cleanup-integration
dotnet .\tools\RunPerformanceDemo.cs prepare-benchmark
dotnet .\tools\RunPerformanceDemo.cs reset-benchmark
dotnet .\tools\RunPerformanceDemo.cs cleanup-benchmark
dotnet .\tools\RunPerformanceDemo.cs benchmark --tool crank --variant 01-inefficient --setup
dotnet .\tools\RunPerformanceDemo.cs benchmark --tool siege --variant 01-inefficient --setup
dotnet .\tools\RunPerformanceDemo.cs build-migration-bundle --version 1.0.0
dotnet .\tools\RunPerformanceDemo.cs migrate-production --bundle .\artifacts\migration-bundles\1.0.0\efbundle.exe --connection "<production connection string>"
```

Omit `--setup` only when an already prepared benchmark database exists.
