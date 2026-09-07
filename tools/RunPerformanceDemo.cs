#!/usr/bin/env dotnet
#:property PublishAot=false
#:package Microsoft.Data.SqlClient@7.0.2
#:include Shared/**/*.cs
#:include Commands/**/*.cs

using MinimalApiPerformanceDemo.Tools;

var context = ToolContext.Create();
var command = args.FirstOrDefault()?.ToLowerInvariant() ?? "menu";

try
{
    switch (command)
    {
        case "menu":
            await BenchmarkCommands.ShowMenuAsync(context);
            break;
        case "prepare-integration":
            await DatabaseCommands.PrepareIntegrationAsync(context);
            break;
        case "cleanup-integration":
            await DatabaseCommands.CleanupIntegrationAsync(context);
            break;
        case "prepare-benchmark":
            await DatabaseCommands.PrepareBenchmarkAsync(context, "01-inefficient");
            break;
        case "reset-benchmark":
            await DatabaseCommands.ResetBenchmarkDataAsync(context, "01-inefficient");
            break;
        case "cleanup-benchmark":
            await DatabaseCommands.CleanupBenchmarkAsync(context);
            break;
        case "siege-config":
            await BenchmarkCommands.ShowSiegeConfigurationAsync(context);
            break;
        case "benchmark":
            await BenchmarkCommands.RunFromArgumentsAsync(context, args[1..]);
            break;
        case "migrate":
            await DatabaseCommands.MigrateAsync(context, "01-inefficient");
            break;
        case "build-migration-bundle":
            await DatabaseCommands.BuildMigrationBundleAsync(context, args[1..]);
            break;
        case "migrate-production":
            await DatabaseCommands.MigrateProductionAsync(context, args[1..]);
            break;
        default:
            throw new InvalidOperationException(
                "Commands: menu, prepare-integration, cleanup-integration, prepare-benchmark, " +
                "reset-benchmark, cleanup-benchmark, siege-config, " +
                "benchmark, migrate, build-migration-bundle, migrate-production.");
    }
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    Environment.ExitCode = 1;
}
